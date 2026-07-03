using System.Text.Json;
using LYBox.Plugin.Downloader.Models;
using LYBox.Plugin.Downloader.Services.Parsers;

namespace LYBox.Plugin.Downloader.Services;

/// <summary>
/// 下载编排器：把解析器/选流器/下载引擎/解密/合并/混流/字幕 串联起来，
/// 完成一次点播下载任务。对应旧 M3u8DownloadService 的角色，但覆盖 HLS/DASH、
/// 多轨道、解密、混流、save-pattern 等全部能力。
/// </summary>
public class DownloadOrchestrator
{
    private readonly DirectUiLogger _logger;
    private readonly DownloadOptions _opts;

    public DownloadOrchestrator(DirectUiLogger logger, DownloadOptions opts)
    {
        _logger = logger;
        _opts = opts;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opts.Input))
            throw new InvalidOperationException("未提供输入链接");

        var saveDir = string.IsNullOrWhiteSpace(_opts.SaveDir) ? Environment.CurrentDirectory : _opts.SaveDir;
        Directory.CreateDirectory(saveDir);
        var saveName = string.IsNullOrWhiteSpace(_opts.SaveName)
            ? $"video_{DateTime.Now:yyyyMMdd_HHmmss}"
            : _opts.SaveName;

        using var client = HttpClientFactory.Create(_opts);
        var throttle = BandwidthThrottle.ParseSpeed(_opts.MaxSpeed) is { } bps and > 0
            ? new BandwidthThrottle(bps) : null;
        var engine = new DownloadEngine(client, _logger, throttle);

        _logger.Log($"正在获取输入: {_opts.Input}");
        var content = await FetchTextAsync(client, _opts.Input, ct);

        var parser = DetectParser(content);
        var media = await parser.ParseAsync(content, _opts.Input, client, ct);
        _logger.Log($"解析完成: {media.Kind}{(media.IsLive ? " (直播)" : "")}，共 {media.Tracks.Count} 条轨道");

        // 直播录制（非 perform-as-vod）交由 LiveRecordEngine
        if (media.IsLive && !_opts.LivePerformAsVod)
        {
            await RecordLiveAsync(parser, media, client, engine, saveDir, saveName, ct);
            return;
        }

        // VOD：选流
        var selected = StreamSelector.Select(
            media.Tracks, _opts.SelectVideo, _opts.SelectAudio, _opts.SelectSubtitle,
            _opts.AutoSelect, _opts.SubOnly);
        _logger.Log($"已选 {selected.Count} 条轨道:");
        foreach (var t in selected) _logger.Log($"  - {t}");

        if (_opts.SkipDownload)
        {
            _logger.Log("已跳过下载 (--skip-download)");
            return;
        }

        // 下载各轨道
        var mergedFiles = new List<(MediaType Type, string Path)>();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var workDir = Path.Combine(saveDir, $".tmp_{timestamp}");
        Directory.CreateDirectory(workDir);

        try
        {
            DecryptionProvider? decryptor = null;
            if (media.Encryption is not null && media.Encryption.Method is not "NONE")
            {
                _opts.Encryption = media.Encryption;
                decryptor = new DecryptionProvider(_logger, _opts);
                _logger.Log($"加密流: {media.Encryption.Method}");
            }

            foreach (var track in selected)
            {
                var (segments, trackEnc) = await GetSegmentsAsync(parser, media, track, content, client, ct);
                segments = DownloadEngine.ApplyCustomRange(segments, _opts.CustomRange);
                if (segments.Count == 0) { _logger.Log($"轨道 {track} 无分片，跳过"); continue; }
                _logger.Log($"轨道 {track.MediaType}: {segments.Count} 个分片");

                // 按轨道构建解密器（子清单可能含 EXT-X-KEY）
                IDecryptionProvider? trackDecryptor = decryptor;
                var effectiveEnc = trackEnc ?? media.Encryption;
                if (effectiveEnc is not null && effectiveEnc.Method is not "NONE")
                {
                    _opts.Encryption = effectiveEnc;
                    trackDecryptor = new DecryptionProvider(_logger, _opts);
                    _logger.Log($"轨道 {track.MediaType} 加密: {effectiveEnc.Method}");
                }

                var ext = track.MediaType switch
                {
                    MediaType.Video => "ts",
                    MediaType.Audio => "m4a",
                    MediaType.Subtitle => "vtt",
                    _ => "ts"
                };

                var init = await engine.DownloadInitAsync(segments.FirstOrDefault()?.InitUrl, ct);
                var tempTrackDir = Path.Combine(workDir, track.MediaType.ToString().ToLowerInvariant());
                Directory.CreateDirectory(tempTrackDir);

                var downloaded = await engine.DownloadAllAsync(
                    segments, init, tempTrackDir, ext, trackDecryptor,
                    _opts.ThreadCount, _opts.RetryCount,
                    (c, t, b) => _logger.Log($"  进度: {c}/{t} ({FormatSize(b)})"),
                    ct);

                var segFiles = downloaded.OrderBy(d => d.Index).Select(d => d.Path).ToList();

                // 字幕特殊处理：合并为 VTT，可转 SRT
                if (track.MediaType == MediaType.Subtitle)
                {
                    var vtt = SubtitleService.MergeVttSegments(segFiles, _logger);
                    if (_opts.AutoSubtitleFix) vtt = SubtitleService.AutoFixTimestamps(vtt);
                    var subOut = _opts.SubFormat.Equals("SRT", StringComparison.OrdinalIgnoreCase)
                        ? SubtitleService.VttToSrt(vtt) : vtt;
                    var subPath = Path.Combine(workDir, $"{saveName}.{track.Language ?? "subtitle"}.{_opts.SubFormat.ToLowerInvariant()}");
                    await File.WriteAllTextAsync(subPath, subOut, ct);
                    mergedFiles.Add((MediaType.Subtitle, subPath));
                    continue;
                }

                // 合并分片
                var mergedPath = Path.Combine(workDir, $"{track.MediaType.ToString().ToLowerInvariant()}.{ext}");
                var merger = new MergeService(_logger, _opts.FfmpegPath);
                await merger.MergeAsync(segFiles, mergedPath, _opts.BinaryMerge, _opts.UseFfmpegConcatDemuxer, _opts.SkipMerge, ct);
                mergedFiles.Add((track.MediaType, mergedPath));
            }

            // 最终输出：混流或单文件
            var finalOutput = await ProduceFinalOutputAsync(mergedFiles, saveDir, saveName, workDir, ct);
            _logger.Log($"完成: {finalOutput}");

            if (_opts.WriteMetaJson)
                await WriteMetaJsonAsync(workDir, media, selected, finalOutput, ct);
        }
        finally
        {
            if (_opts.DelAfterDone && Directory.Exists(workDir))
            {
                try { Directory.Delete(workDir, true); } catch (Exception ex) { _logger.Log($"[Cleanup] {ex.Message}"); }
            }
        }
    }

    private async Task RecordLiveAsync(
        IPlaylistParser parser, ParsedMedia media, HttpClient client, DownloadEngine engine,
        string saveDir, string saveName, CancellationToken ct)
    {
        var outputPath = Path.Combine(saveDir, saveName + ".ts");
        var hlsParser = parser as HlsParser;
        var dashParser = parser as DashParser;

        var liveEngine = new LiveRecordEngine(client, _logger, engine, null);

        Func<CancellationToken, Task<List<MediaSegment>>> provider = null!;
        if (hlsParser is not null)
        {
            // 直播清单 URL = 第一条轨道的 URL（media playlist）；master 的话需先选定
            var mediaUrl = media.Kind == MediaKind.HlsMaster
                ? media.Tracks.FirstOrDefault(t => t.MediaType == MediaType.Video)?.Url ?? media.Tracks[0].Url
                : media.InputUrl;
            provider = async token =>
            {
                var text = await FetchTextAsync(client, mediaUrl, token);
                return HlsParser.ParseSegments(text, mediaUrl);
            };
        }
        else if (dashParser is not null)
        {
            var track = media.Tracks.FirstOrDefault(t => t.MediaType == MediaType.Video) ?? media.Tracks[0];
            provider = async token => await dashParser.GetSegmentsAsync(track, client, token);
        }
        else
        {
            throw new NotSupportedException("该源类型不支持直播录制");
        }

        await liveEngine.RecordAsync(provider, outputPath, _opts, ct);
    }

    private async Task<(List<MediaSegment> Segments, EncryptionInfo? Encryption)> GetSegmentsAsync(
        IPlaylistParser parser, ParsedMedia media, StreamTrack track, string masterContent, HttpClient client, CancellationToken ct)
    {
        if (media.Kind == MediaKind.HlsMaster)
        {
            // track.Url 指向子 playlist，重新获取并解析
            var sub = await FetchTextAsync(client, track.Url, ct);
            var subMedia = await parser.ParseAsync(sub, track.Url, client, ct);
            return (HlsParser.ParseSegments(sub, track.Url), subMedia.Encryption);
        }
        if (media.Kind == MediaKind.HlsMedia)
        {
            return (HlsParser.ParseSegments(masterContent, media.InputUrl), media.Encryption);
        }
        if (media.Kind == MediaKind.Dash && parser is DashParser dash)
        {
            return (await dash.GetSegmentsAsync(track, client, ct), media.Encryption);
        }
        throw new NotSupportedException($"不支持获取分片: {media.Kind}");
    }

    private async Task<string> ProduceFinalOutputAsync(
        List<(MediaType Type, string Path)> merged, string saveDir, string saveName, string workDir, CancellationToken ct)
    {
        if (merged.Count == 0)
            throw new InvalidOperationException("无任何可输出文件");

        // 单轨道：直接移动到保存目录
        if (merged.Count == 1 || _opts.MuxAfterDone is null)
        {
            var src = merged[0].Path;
            var ext = Path.GetExtension(src);
            var outPath = Path.Combine(saveDir, ApplySavePattern(saveName, ext, merged[0].Type));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            File.Move(src, outPath, overwrite: true);
            return outPath;
        }

        // 混流
        var primary = merged.First(m => m.Type == MediaType.Video).Path;
        var additional = merged.Where(m => m.Type != MediaType.Video).Select(m => m.Path).ToList();
        var muxer = new MuxService(_logger, _opts.FfmpegPath, _opts.MkvmergePath);
        var outWithoutExt = Path.Combine(saveDir, saveName);
        return await muxer.MuxAsync(primary, additional, outWithoutExt, _opts.MuxAfterDone!, _opts.MuxImports, ct);
    }

    /// <summary>应用 --save-pattern 命名模板（简化：仅替换 Resolution/Bandwidth/Language/MediaType）</summary>
    private string ApplySavePattern(string saveName, string ext, MediaType type)
    {
        if (string.IsNullOrWhiteSpace(_opts.SavePattern)) return saveName + ext;
        var name = _opts.SavePattern
            .Replace("<SaveName>", saveName)
            .Replace("<MediaType>", type.ToString())
            .Replace("<Ext>", ext.TrimStart('.'));
        return name + ext;
    }

    private async Task WriteMetaJsonAsync(string dir, ParsedMedia media, List<StreamTrack> selected, string output, CancellationToken ct)
    {
        try
        {
            var meta = new
            {
                input = media.InputUrl,
                kind = media.Kind.ToString(),
                isLive = media.IsLive,
                encryption = media.Encryption?.Method,
                selectedTracks = selected.Select(t => new
                {
                    type = t.MediaType.ToString(),
                    t.Resolution, t.Bandwidth, t.Language, t.Codecs
                }),
                output
            };
            var path = Path.Combine(dir, "meta.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }), ct);
        }
        catch { /* 元数据写入失败不影响主流程 */ }
    }

    private static IPlaylistParser DetectParser(string content)
    {
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) && content.Contains("<MPD", StringComparison.OrdinalIgnoreCase))
            return new DashParser();
        if (content.Contains("<SmoothStreamingMedia", StringComparison.OrdinalIgnoreCase))
            return new MssParser();
        if (content.Contains("#EXTM3U", StringComparison.OrdinalIgnoreCase))
            return new HlsParser();
        throw new NotSupportedException("无法识别的输入格式（仅支持 HLS / DASH / MSS）");
    }

    private static async Task<string> FetchTextAsync(HttpClient client, string url, CancellationToken ct)
    {
        // 本地文件输入（解密混流页等场景）
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.IsFile)
        {
            var localPath = uri?.LocalPath ?? url;
            if (File.Exists(localPath))
                return await File.ReadAllTextAsync(localPath, ct);
        }
        var resp = await client.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1_000_000_000) return $"{bytes / 1_000_000_000.0:F2} GB";
        if (bytes >= 1_000_000) return $"{bytes / 1_000_000.0:F2} MB";
        if (bytes >= 1_000) return $"{bytes / 1_000.0:F2} KB";
        return $"{bytes} B";
    }
}
