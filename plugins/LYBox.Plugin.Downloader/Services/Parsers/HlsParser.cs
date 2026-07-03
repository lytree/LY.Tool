using System.Text.RegularExpressions;
using LYBox.Plugin.Downloader.Models;

namespace LYBox.Plugin.Downloader.Services.Parsers;

/// <summary>
/// HLS (m3u8) 解析器：支持 master playlist（EXT-X-STREAM-INF + EXT-X-MEDIA）
/// 与 media playlist（EXT-X-KEY / EXT-X-MAP / EXTINF / 直播检测）。
/// 对应 N_m3u8DL-RE 的 HLS 解析能力。
/// </summary>
public class HlsParser : IPlaylistParser
{
    private static readonly Regex AttrRegex = new(@"([A-Z0-9-]+)=([^,]+(?:,[^,=]+)*?)(?=(?:,[A-Z0-9-]+=)|$)", RegexOptions.Compiled);

    public bool CanParse(string content)
        => content.Contains("#EXTM3U", StringComparison.OrdinalIgnoreCase);

    public async Task<ParsedMedia> ParseAsync(string content, string baseUrl, HttpClient client, CancellationToken ct)
    {
        var lines = content.Split('\n');
        var hasStreamInf = lines.Any(l => l.TrimStart().StartsWith("#EXT-X-STREAM-INF:"));

        if (hasStreamInf)
            return ParseMaster(content, baseUrl);
        return await ParseMediaAsync(content, baseUrl, client, ct);
    }

    /// <summary>解析 master playlist，产出视频/音频/字幕轨道列表</summary>
    private ParsedMedia ParseMaster(string content, string m3u8Url)
    {
        var tracks = new List<StreamTrack>();
        var lines = content.Split('\n');
        var audioGroups = new Dictionary<string, List<(string Lang, string Name, string? Uri)>>(); // GROUP-ID → 媒体
        var subtitleGroups = new Dictionary<string, List<(string Lang, string Name, string? Uri)>>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.StartsWith("#EXT-X-MEDIA:"))
            {
                var attrs = ParseAttributes(line["#EXT-X-MEDIA:".Length..]);
                var type = attrs.GetValueOrDefault("TYPE");
                var groupId = attrs.GetValueOrDefault("GROUP-ID");
                var lang = attrs.GetValueOrDefault("LANGUAGE");
                var name = attrs.GetValueOrDefault("NAME");
                var uri = attrs.GetValueOrDefault("URI");

                if (groupId is null) continue;

                if (type == "AUDIO")
                    AddGroup(audioGroups, groupId, (lang ?? "", name ?? "", uri));
                else if (type == "SUBTITLES")
                    AddGroup(subtitleGroups, groupId, (lang ?? "", name ?? "", uri));
            }
            else if (line.StartsWith("#EXT-X-STREAM-INF:"))
            {
                var attrs = ParseAttributes(line["#EXT-X-STREAM-INF:".Length..]);
                if (i + 1 >= lines.Length) continue;
                var urlLine = lines[i + 1].Trim();
                if (string.IsNullOrEmpty(urlLine) || urlLine.StartsWith('#')) continue;

                var resolved = UrlUtils.Resolve(m3u8Url, urlLine);
                long.TryParse(attrs.GetValueOrDefault("BANDWIDTH"), out var bw);
                var res = attrs.GetValueOrDefault("RESOLUTION");
                var codecs = attrs.GetValueOrDefault("CODECS");
                double.TryParse(attrs.GetValueOrDefault("FRAME-RATE"), out var fps);
                var videoRange = attrs.GetValueOrDefault("VIDEO-RANGE");

                tracks.Add(new StreamTrack(
                    MediaType.Video, resolved,
                    Codecs: codecs, Resolution: res, Bandwidth: bw,
                    FrameRate: fps > 0 ? fps : null, VideoRange: videoRange));

                // 关联音频/字幕组（仅记录 URI，后续按需展开）
                if (attrs.TryGetValue("AUDIO", out var audioGroup) && audioGroups.TryGetValue(audioGroup, out var audios))
                {
                    foreach (var a in audios)
                    {
                        if (a.Uri is null) continue;
                        tracks.Add(new StreamTrack(
                            MediaType.Audio, UrlUtils.Resolve(m3u8Url, a.Uri),
                            Language: a.Lang, Name: a.Name, Codecs: codecs));
                    }
                }
                if (attrs.TryGetValue("SUBTITLES", out var subGroup) && subtitleGroups.TryGetValue(subGroup, out var subs))
                {
                    foreach (var s in subs)
                    {
                        if (s.Uri is null) continue;
                        tracks.Add(new StreamTrack(
                            MediaType.Subtitle, UrlUtils.Resolve(m3u8Url, s.Uri),
                            Language: s.Lang, Name: s.Name));
                    }
                }
            }
        }

        return new ParsedMedia(m3u8Url, MediaKind.HlsMaster, IsLive: false, Tracks: tracks);
    }

    private static void AddGroup(Dictionary<string, List<(string, string, string?)>> dict, string key, (string, string, string?) val)
    {
        if (!dict.TryGetValue(key, out var list)) dict[key] = list = [];
        list.Add(val);
    }

    /// <summary>解析 media playlist，产出单轨道（含分片、加密、直播标志）</summary>
    private async Task<ParsedMedia> ParseMediaAsync(string content, string m3u8Url, HttpClient client, CancellationToken ct)
    {
        EncryptionInfo? enc = null;
        var isLive = true;
        var lines = content.Split('\n');
        double? curDur = null;
        var initUrl = (string?)null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.StartsWith("#EXT-X-ENDLIST", StringComparison.OrdinalIgnoreCase))
            {
                isLive = false;
            }
            else if (line.StartsWith("#EXT-X-KEY:", StringComparison.OrdinalIgnoreCase))
            {
                var attrs = ParseAttributes(line["#EXT-X-KEY:".Length..]);
                var method = attrs.GetValueOrDefault("METHOD") ?? "NONE";
                var uri = attrs.GetValueOrDefault("URI");
                var iv = attrs.GetValueOrDefault("IV")?[2..]; // 去掉 0x
                var keyFormat = attrs.GetValueOrDefault("KEYFORMAT");
                var keyId = attrs.GetValueOrDefault("KEYID")?[2..];

                enc = new EncryptionInfo(
                    Method: method,
                    KeyUrl: uri is null ? null : UrlUtils.Resolve(m3u8Url, uri),
                    IvHex: iv,
                    Kid: keyId,
                    KeyFormat: keyFormat);

                if (uri is not null && method is "AES-128" or "AES-128-ECB" or "CHACHA20")
                {
                    try
                    {
                        var resp = await client.GetAsync(enc.KeyUrl, ct);
                        resp.EnsureSuccessStatusCode();
                        enc = enc with { Key = await resp.Content.ReadAsByteArrayAsync(ct) };
                    }
                    catch { /* 密钥获取失败，留给后续用用户提供的 key 处理 */ }
                }
            }
            else if (line.StartsWith("#EXT-X-MAP:", StringComparison.OrdinalIgnoreCase))
            {
                var attrs = ParseAttributes(line["#EXT-X-MAP:".Length..]);
                initUrl = attrs.GetValueOrDefault("URI") is { } u ? UrlUtils.Resolve(m3u8Url, u) : null;
            }
            else if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                var durStr = line["#EXTINF:".Length..];
                var comma = durStr.IndexOf(',');
                if (comma >= 0) durStr = durStr[..comma];
                double.TryParse(durStr, out var d);
                curDur = d;
            }
            // 分片 URL 行在调用方按需收集；此处仅检测 live/key
        }

        // 单轨道占位（URL 即 m3u8 本身）
        var track = new StreamTrack(MediaType.Video, m3u8Url);
        return new ParsedMedia(m3u8Url, MediaKind.HlsMedia, IsLive: isLive, Tracks: [track], Encryption: enc);
    }

    /// <summary>解析 media playlist 的分片列表（轨道级）</summary>
    public static List<MediaSegment> ParseSegments(string content, string m3u8Url)
    {
        var segments = new List<MediaSegment>();
        var lines = content.Split('\n');
        double? curDur = null;
        string? initUrl = null;
        var discontinuity = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith('#'))
            {
                if (line.StartsWith("#EXT-X-DISCONTINUITY", StringComparison.OrdinalIgnoreCase))
                    discontinuity = true;
                else if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
                {
                    var durStr = line["#EXTINF:".Length..];
                    var comma = durStr.IndexOf(',');
                    if (comma >= 0) durStr = durStr[..comma];
                    double.TryParse(durStr, out var d);
                    curDur = d;
                }
                else if (line.StartsWith("#EXT-X-MAP:", StringComparison.OrdinalIgnoreCase))
                {
                    var attrs = ParseAttributes(line["#EXT-X-MAP:".Length..]);
                    initUrl = attrs.GetValueOrDefault("URI") is { } u ? UrlUtils.Resolve(m3u8Url, u) : null;
                }
            }
            else
            {
                segments.Add(new MediaSegment(
                    Index: segments.Count,
                    Url: UrlUtils.Resolve(m3u8Url, line),
                    DurationSeconds: curDur,
                    IsDiscontinuity: discontinuity,
                    InitUrl: initUrl));
                curDur = null;
                discontinuity = false;
            }
        }
        return segments;
    }

    /// <summary>解析属性键值对，正确处理带引号值</summary>
    private static Dictionary<string, string> ParseAttributes(string attrString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // 先按 KEY=VALUE 拆分，VALUE 可能含引号
        var matches = Regex.Matches(attrString, @"([A-Z0-9-]+)=((?:\""[^\""]*\""|[^,]*))");
        foreach (Match m in matches)
        {
            var key = m.Groups[1].Value;
            var val = m.Groups[2].Value.Trim('"');
            result[key] = val;
        }
        return result;
    }
}
