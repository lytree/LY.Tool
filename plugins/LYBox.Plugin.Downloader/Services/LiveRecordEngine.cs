using LYBox.Plugin.Downloader.Models;

namespace LYBox.Plugin.Downloader.Services;

/// <summary>
/// 直播录制引擎：对应 N_m3u8DL-RE 的直播录制能力。
/// --live-real-time-merge（实时合并到单一文件）、--live-pipe-mux（管道+ffmpeg 实时混流到 TS）、
/// --live-keep-segments（保留分片）、--live-record-limit（录制时长上限）、
/// --live-wait-time（刷新间隔）、--live-take-count（首次抓取数）、--live-perform-as-vod（以点播方式下载）。
/// </summary>
public class LiveRecordEngine
{
    private readonly HttpClient _client;
    private readonly DirectUiLogger _logger;
    private readonly DownloadEngine _engine;
    private readonly IDecryptionProvider? _decryptor;

    // 录制会话级状态（避免 async 方法无法使用 ref 参数）
    private int _segmentIndex;
    private long _totalBytes;

    public LiveRecordEngine(HttpClient client, DirectUiLogger logger, DownloadEngine engine, IDecryptionProvider? decryptor)
    {
        _client = client;
        _logger = logger;
        _engine = engine;
        _decryptor = decryptor;
    }

    /// <summary>
    /// 录制直播流。
    /// </summary>
    /// <param name="segmentProvider">每次调用返回当前最新的分片列表（调用方负责刷新清单）</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="opts">下载选项</param>
    /// <param name="ct">取消令牌</param>
    public async Task RecordAsync(
        Func<CancellationToken, Task<List<MediaSegment>>> segmentProvider,
        string outputPath,
        DownloadOptions opts,
        CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
        var tempDir = Path.Combine(Path.GetDirectoryName(outputPath) ?? ".", $".live_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        Directory.CreateDirectory(tempDir);

        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _segmentIndex = 0;
        _totalBytes = 0;
        var startTime = DateTime.Now;

        // 实时合并输出流
        FileStream? mergeStream = null;
        if (opts.LiveRealTimeMerge && !opts.LivePipeMux)
        {
            mergeStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        }

        // pipe-mux：通过 ffmpeg stdin 实时混流为 TS
        System.Diagnostics.Process? pipeProc = null;
        Stream? pipeStdin = null;
        if (opts.LivePipeMux)
        {
            (pipeProc, pipeStdin) = StartPipeMux(outputPath, opts);
            _logger.Log("已启动 ffmpeg 管道实时混流 (--live-pipe-mux)");
        }

        try
        {
            // perform-as-vod：仅抓取一次后退出
            if (opts.LivePerformAsVod)
            {
                _logger.Log("以点播方式下载直播流 (--live-perform-as-vod)");
                var segs = await segmentProvider(ct);
                await DownloadBatchAsync(segs, tempDir, seenUrls, opts, ct, mergeStream, pipeStdin);
                return;
            }

            // 主循环
            int consecutiveEmpty = 0;
            while (!ct.IsCancellationRequested)
            {
                // 录制时长限制
                if (opts.LiveRecordLimit is { } limit)
                {
                    var elapsed = DateTime.Now - startTime;
                    if (elapsed >= limit)
                    {
                        _logger.Log($"达到录制时长限制 ({limit:hh\\:mm\\:ss})");
                        break;
                    }
                }

                List<MediaSegment> current;
                try { current = await segmentProvider(ct); }
                catch (Exception ex)
                {
                    _logger.Log($"[Live] 刷新清单失败: {ex.Message}");
                    await Task.Delay(Math.Max(1000, (opts.LiveWaitTime ?? 5) * 1000), ct);
                    continue;
                }

                var newSegs = current.Where(s => seenUrls.Add(s.Url)).ToList();
                if (newSegs.Count == 0)
                {
                    consecutiveEmpty++;
                    // 连续 30 次空且非直播 → 结束
                    if (consecutiveEmpty > 30 && !await IsStillLiveAsync(ct))
                    {
                        _logger.Log("直播流已结束 (ENDLIST)");
                        break;
                    }
                    await Task.Delay(Math.Max(1000, (opts.LiveWaitTime ?? 5) * 1000), ct);
                    continue;
                }
                consecutiveEmpty = 0;

                // 首次只取 LiveTakeCount 个
                if (_segmentIndex == 0 && newSegs.Count > opts.LiveTakeCount)
                    newSegs = newSegs.Take(opts.LiveTakeCount).ToList();

                await DownloadBatchAsync(newSegs, tempDir, seenUrls, opts, ct, mergeStream, pipeStdin);
            }
        }
        finally
        {
            mergeStream?.Flush();
            mergeStream?.Dispose();

            if (pipeStdin is not null)
            {
                try { pipeStdin.Flush(); pipeStdin.Close(); } catch { }
                try { pipeProc?.WaitForExit(5000); } catch { }
                pipeProc?.Dispose();
            }

            // 清理分片
            if (!opts.LiveKeepSegments && Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }

            var elapsed = DateTime.Now - startTime;
            _logger.Log($"直播录制结束，已录制 {FormatSize(_totalBytes)}，时长 {elapsed:hh\\:mm\\:ss}");
        }
    }

    private async Task DownloadBatchAsync(
        List<MediaSegment> segs, string tempDir, HashSet<string> seen,
        DownloadOptions opts, CancellationToken ct,
        FileStream? mergeStream, Stream? pipeStdin)
    {
        foreach (var seg in segs)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var data = await _engine.DownloadDataAsync(seg.Url, ct);
                if (_decryptor is not null && !_decryptor.NeedsFileBasedDecryption)
                    data = _decryptor.DecryptSegment(data, seg.Index, null);

                // 落盘（若 keep-segments）
                if (opts.LiveKeepSegments)
                {
                    var filePath = Path.Combine(tempDir, $"{_segmentIndex:D5}.ts");
                    await File.WriteAllBytesAsync(filePath, data, ct);
                }

                // 实时合并
                mergeStream?.Write(data, 0, data.Length);
                pipeStdin?.Write(data, 0, data.Length);
                pipeStdin?.Flush();

                Interlocked.Add(ref _totalBytes, data.Length);
                _segmentIndex++;
                if (_segmentIndex % 20 == 0)
                    _logger.Log($"[Live] 已录制 {_segmentIndex} 个分片 ({FormatSize(_totalBytes)})");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _logger.Log($"[Live] 分片下载失败 #{seg.Index}: {ex.Message}"); }
        }
    }

    private async Task<bool> IsStillLiveAsync(CancellationToken ct)
    {
        // 简化：假定仍为直播。具体由调用方在 segmentProvider 中检测 ENDLIST。
        await Task.Delay(100, ct);
        return true;
    }

    private (System.Diagnostics.Process proc, Stream stdin) StartPipeMux(string outputPath, DownloadOptions opts)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = string.IsNullOrWhiteSpace(opts.FfmpegPath) ? "ffmpeg" : opts.FfmpegPath,
            Arguments = $"-y -f mpegts -i pipe:0 -c copy \"{outputPath}\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var proc = System.Diagnostics.Process.Start(psi) ?? throw new InvalidOperationException("无法启动 ffmpeg");
        // 异步消费 stderr/stdout 避免死锁
        _ = Task.Run(() =>
        {
            string? line;
            while ((line = proc.StandardError.ReadLine()) is not null)
            {
                if (!string.IsNullOrEmpty(line)) _logger.Log($"[FFmpeg] {line}");
            }
        });
        return (proc, proc.StandardInput.BaseStream);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1_000_000_000) return $"{bytes / 1_000_000_000.0:F2} GB";
        if (bytes >= 1_000_000) return $"{bytes / 1_000_000.0:F2} MB";
        if (bytes >= 1_000) return $"{bytes / 1_000.0:F2} KB";
        return $"{bytes} B";
    }
}
