using System.Globalization;
using System.Xml.Linq;
using LYBox.Plugin.Downloader.Models;

namespace LYBox.Plugin.Downloader.Services.Parsers;

/// <summary>
/// DASH (MPD) 解析器。支持 static / dynamic 类型、多 Period、SegmentTemplate（含 SegmentTimeline）、
/// SegmentList、SegmentBase、ContentProtection（KID 提取）。
/// 对应 N_m3u8DL-RE 的 DASH 解析能力。
/// </summary>
public class DashParser : IPlaylistParser
{
    private const string DashNs = "urn:mpeg:dash:schema:mpd:2011";
    private XDocument? _doc;
    private string _baseUrl = string.Empty;

    public bool CanParse(string content)
        => content.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) &&
           content.Contains("<MPD", StringComparison.OrdinalIgnoreCase);

    public Task<ParsedMedia> ParseAsync(string content, string baseUrl, HttpClient client, CancellationToken ct)
    {
        _doc = XDocument.Parse(content);
        _baseUrl = baseUrl;
        var mpd = _doc.Root ?? throw new InvalidOperationException("无效 MPD 文档");
        var typeAttr = mpd.Attribute("type")?.Value ?? "static";
        var isLive = typeAttr.Equals("dynamic", StringComparison.OrdinalIgnoreCase);

        var tracks = new List<StreamTrack>();
        EncryptionInfo? enc = null;

        var periods = mpd.Descendants().Where(e => e.Name.LocalName == "Period").ToList();
        foreach (var period in periods)
        {
            var adaptationSets = period.Descendants().Where(e => e.Name.LocalName == "AdaptationSet").ToList();
            foreach (var set in adaptationSets)
            {
                var contentType = set.Attribute("contentType")?.Value
                                  ?? set.Attribute("mimeType")?.Value
                                  ?? "video";
                var lang = set.Attribute("lang")?.Value;
                var mediaType = ClassifyMediaType(contentType);

                // ContentProtection（CENC）
                var cp = set.Descendants().FirstOrDefault(e => e.Name.LocalName == "ContentProtection");
                if (cp is not null)
                {
                    var kid = cp.Attribute("KID")?.Value ?? cp.Descendants()
                        .FirstOrDefault(d => d.Name.LocalName == "cenc:default_KID")?.Value;
                    if (kid is not null && enc is null)
                        enc = new EncryptionInfo(Method: "CENC", Kid: kid.Replace("-", ""));
                }

                var representations = set.Descendants().Where(e => e.Name.LocalName == "Representation").ToList();
                foreach (var rep in representations)
                {
                    var track = BuildTrack(rep, mediaType, lang, set);
                    if (track is not null) tracks.Add(track);
                }
            }
        }

        return Task.FromResult(new ParsedMedia(baseUrl, MediaKind.Dash, IsLive: isLive, Tracks: tracks, Encryption: enc));
    }

    private static MediaType ClassifyMediaType(string contentType)
    {
        if (contentType.Contains("video", StringComparison.OrdinalIgnoreCase)) return MediaType.Video;
        if (contentType.Contains("audio", StringComparison.OrdinalIgnoreCase)) return MediaType.Audio;
        if (contentType.Contains("text", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("subtitle", StringComparison.OrdinalIgnoreCase)) return MediaType.Subtitle;
        return MediaType.Video;
    }

    private StreamTrack? BuildTrack(XElement rep, MediaType mediaType, string? lang, XElement adaptationSet)
    {
        var id = rep.Attribute("id")?.Value ?? "";
        var bwStr = rep.Attribute("bandwidth")?.Value;
        long.TryParse(bwStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var bw);
        var codecs = rep.Attribute("codecs")?.Value ?? adaptationSet.Attribute("codecs")?.Value;
        var widthStr = rep.Attribute("width")?.Value;
        var heightStr = rep.Attribute("height")?.Value;
        var res = (widthStr is not null && heightStr is not null) ? $"{widthStr}x{heightStr}" : null;
        double.TryParse(rep.Attribute("frameRate")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var fps);
        var channels = int.TryParse(rep.Attribute("numChannels")?.Value, out var ch) ? ch : (int?)null;

        // BaseURL 直接作为单分片
        var baseUrlChild = rep.Descendants().FirstOrDefault(e => e.Name.LocalName == "BaseURL");
        if (baseUrlChild is not null)
        {
            var url = UrlUtils.Resolve(_baseUrl, baseUrlChild.Value);
            return new StreamTrack(mediaType, url, Id: id, Language: lang, Codecs: codecs,
                Resolution: res, Bandwidth: bw > 0 ? bw : null,
                FrameRate: fps > 0 ? fps : null, Channels: channels,
                SegmentCount: 1);
        }

        // SegmentTemplate / SegmentList / SegmentBase 优先从 adaptationSet 取（共享模板），再从 rep 取
        var template = rep.Descendants().FirstOrDefault(e => e.Name.LocalName == "SegmentTemplate")
                       ?? adaptationSet.Descendants().FirstOrDefault(e => e.Name.LocalName == "SegmentTemplate");
        var list = rep.Descendants().FirstOrDefault(e => e.Name.LocalName == "SegmentList")
                   ?? adaptationSet.Descendants().FirstOrDefault(e => e.Name.LocalName == "SegmentList");
        var segBase = rep.Descendants().FirstOrDefault(e => e.Name.LocalName == "SegmentBase")
                      ?? adaptationSet.Descendants().FirstOrDefault(e => e.Name.LocalName == "SegmentBase");

        if (template is not null)
        {
            var mediaTpl = template.Attribute("media")?.Value;
            var initTpl = template.Attribute("initialization")?.Value;
            var url = mediaTpl ?? initTpl ?? _baseUrl;
            // 模板 URL 保留占位符，GetSegmentsAsync 再展开
            return new StreamTrack(mediaType, UrlUtils.Resolve(_baseUrl, url), Id: id, Language: lang,
                Codecs: codecs, Resolution: res, Bandwidth: bw > 0 ? bw : null,
                FrameRate: fps > 0 ? fps : null, Channels: channels);
        }

        if (list is not null)
        {
            var first = list.Descendants().FirstOrDefault(e => e.Name.LocalName == "SegmentURL");
            var url = first?.Attribute("media")?.Value ?? _baseUrl;
            return new StreamTrack(mediaType, UrlUtils.Resolve(_baseUrl, url), Id: id, Language: lang,
                Codecs: codecs, Resolution: res, Bandwidth: bw > 0 ? bw : null,
                FrameRate: fps > 0 ? fps : null, Channels: channels);
        }

        if (segBase is not null)
        {
            var init = segBase.Descendants().FirstOrDefault(e => e.Name.LocalName == "Initialization");
            var url = init?.Attribute("sourceURL")?.Value ?? _baseUrl;
            return new StreamTrack(mediaType, UrlUtils.Resolve(_baseUrl, url), Id: id, Language: lang,
                Codecs: codecs, Resolution: res, Bandwidth: bw > 0 ? bw : null,
                FrameRate: fps > 0 ? fps : null, Channels: channels, SegmentCount: 1);
        }

        return null;
    }

    /// <summary>为指定 DASH 轨道生成分片列表（展开 $Number$ / $Time$ 模板）</summary>
    public Task<List<MediaSegment>> GetSegmentsAsync(StreamTrack track, HttpClient client, CancellationToken ct)
    {
        if (_doc is null) return Task.FromResult(new List<MediaSegment>());

        var rep = FindRepresentation(track.Id);
        if (rep is null) return Task.FromResult(new List<MediaSegment>());

        var adaptationSet = rep.Parent;
        var template = rep.Descendants().FirstOrDefault(e => e.Name.LocalName == "SegmentTemplate")
                       ?? adaptationSet?.Descendants().FirstOrDefault(e => e.Name.LocalName == "SegmentTemplate");

        if (template is not null)
        {
            var mediaTpl = template.Attribute("media")?.Value ?? "$Number$";
            var initTpl = template.Attribute("initialization")?.Value;
            var timescale = long.TryParse(template.Attribute("timescale")?.Value, out var ts) ? ts : 1;
            var duration = long.TryParse(template.Attribute("duration")?.Value, out var d) ? d : 0;
            var startNumber = long.TryParse(template.Attribute("startNumber")?.Value, out var sn) ? sn : 1;

            string? initUrl = initTpl is null ? null : UrlUtils.Resolve(_baseUrl,
                Substitute(initTpl, track, number: startNumber, time: 0));

            var segments = new List<MediaSegment>();

            var timeline = template.Descendants().FirstOrDefault(e => e.Name.LocalName == "SegmentTimeline");
            if (timeline is not null)
            {
                long t = 0;
                long number = startNumber;
                foreach (var s in timeline.Descendants().Where(e => e.Name.LocalName == "S"))
                {
                    var tAttr = s.Attribute("t");
                    if (tAttr is not null && long.TryParse(tAttr.Value, out var tt)) t = tt;
                    var dAttr = s.Attribute("d");
                    if (dAttr is null || !long.TryParse(dAttr.Value, out var sd) || sd <= 0) continue;
                    var rAttr = s.Attribute("r");
                    var repeat = int.TryParse(rAttr?.Value, out var rv) ? rv : 0;

                    for (int k = 0; k <= repeat; k++)
                    {
                        var url = UrlUtils.Resolve(_baseUrl, Substitute(mediaTpl, track, number: number, time: t));
                        segments.Add(new MediaSegment(
                            Index: segments.Count, Url: url,
                            DurationSeconds: timescale > 0 ? (double)sd / timescale : null,
                            InitUrl: initUrl));
                        t += sd;
                        number++;
                    }
                }
            }
            else if (duration > 0 && timescale > 0)
            {
                // 由 mediaPresentationDuration 推导分片数
                var mpdDurationSeconds = GetPresentationDurationSeconds();
                if (mpdDurationSeconds > 0)
                {
                    var segDur = (double)duration / timescale;
                    var count = (int)Math.Ceiling(mpdDurationSeconds / segDur);
                    for (long n = 0; n < count; n++)
                    {
                        var num = startNumber + n;
                        var url = UrlUtils.Resolve(_baseUrl, Substitute(mediaTpl, track, number: num, time: num * duration));
                        segments.Add(new MediaSegment(
                            Index: (int)n, Url: url,
                            DurationSeconds: segDur, InitUrl: initUrl));
                    }
                }
                else
                {
                    // 无 duration 信息，至少产出首个分片供尝试
                    var url = UrlUtils.Resolve(_baseUrl, Substitute(mediaTpl, track, number: startNumber, time: 0));
                    segments.Add(new MediaSegment(Index: 0, Url: url, DurationSeconds: (double)duration / timescale, InitUrl: initUrl));
                }
            }
            return Task.FromResult(segments);
        }

        // SegmentList
        var list = rep.Descendants().FirstOrDefault(e => e.Name.LocalName == "SegmentList")
                   ?? adaptationSet?.Descendants().FirstOrDefault(e => e.Name.LocalName == "SegmentList");
        if (list is not null)
        {
            var segments = new List<MediaSegment>();
            foreach (var s in list.Descendants().Where(e => e.Name.LocalName == "SegmentURL"))
            {
                var media = s.Attribute("media")?.Value;
                if (media is null) continue;
                segments.Add(new MediaSegment(Index: segments.Count, Url: UrlUtils.Resolve(_baseUrl, media)));
            }
            return Task.FromResult(segments);
        }

        // SegmentBase / BaseURL：单分片
        return Task.FromResult(new List<MediaSegment>
        {
            new(0, track.Url)
        });
    }

    private XElement? FindRepresentation(string? id)
    {
        if (_doc is null) return null;
        var reps = _doc.Descendants().Where(e => e.Name.LocalName == "Representation");
        foreach (var r in reps)
        {
            if (r.Attribute("id")?.Value == id) return r;
        }
        return reps.FirstOrDefault();
    }

    private double GetPresentationDurationSeconds()
    {
        var mpd = _doc?.Root;
        if (mpd is null) return 0;
        var dur = mpd.Attribute("mediaPresentationDuration")?.Value;
        return ParseDuration(dur);
    }

    public static double ParseDuration(string? isoDuration)
    {
        if (string.IsNullOrEmpty(isoDuration)) return 0;
        // 形如 PT0H1M30.5S 或 PT90S
        var m = System.Text.RegularExpressions.Regex.Match(isoDuration,
            @"P(?:T)?(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?");
        if (!m.Success) return 0;
        double h = m.Groups[1].Success ? double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        double mn = m.Groups[2].Success ? double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        double s = m.Groups[3].Success ? double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : 0;
        return h * 3600 + mn * 60 + s;
    }

    private static string Substitute(string template, StreamTrack track, long number, long time)
    {
        return template
            .Replace("$RepresentationID$", track.Id ?? "")
            .Replace("$Bandwidth$", (track.Bandwidth ?? 0).ToString())
            .Replace("$Number$", number.ToString())
            .Replace("$Time$", time.ToString());
    }
}
