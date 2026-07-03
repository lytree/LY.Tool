using LYBox.Plugin.Downloader.Models;

namespace LYBox.Plugin.Downloader.Services;

/// <summary>
/// 并发分片下载引擎。对应 N_m3u8DL-RE 的下载核心：
/// --thread-count（并发）、--download-retry-count（重试）、-R（限速）、--custom-range（范围过滤）。
/// 解密由 DecryptionProvider 在分片落盘前处理。
/// </summary>
public class DownloadEngine
{
    private readonly HttpClient _client;
    private readonly DirectUiLogger _logger;
    private readonly BandwidthThrottle? _throttle;

    public DownloadEngine(HttpClient client, DirectUiLogger logger, BandwidthThrottle? throttle = null)
    {
        _client = client;
        _logger = logger;
        _throttle = throttle;
    }

    /// <summary>下载 init segment（EXT-X-MAP / DASH initialization）</summary>
    public async Task<byte[]?> DownloadInitAsync(string? url, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(url)) return null;
        try { return await DownloadDataAsync(url, ct).ConfigureAwait(false); }
        catch (Exception ex) { _logger.Log($"[Init] {ex.Message}"); return null; }
    }

    /// <summary>
    /// 并发下载全部分片到 tempDir，命名为 {index:D5}.{ext}。
    /// 返回成功落盘的 (index, filePath) 列表（按 index 排序）。
    /// </summary>
    public async Task<List<(int Index, string Path, byte[]? InitData)>> DownloadAllAsync(
        List<MediaSegment> segments,
        byte[]? initData,
        string tempDir,
        string extension,
        IDecryptionProvider? decryptor,
        int concurrency,
        int retryCount,
        ProgressCallback progress,
        CancellationToken ct)
    {
        var completed = 0;
        long totalBytes = 0;
        var total = segments.Count;
        var failed = new List<int>();
        var results = new List<(int Index, string Path, byte[]? InitData)>();
        var lockObj = new object();

        using var semaphore = new SemaphoreSlim(Math.Max(1, concurrency));
        var tasks = new List<Task>(total);

        foreach (var segment in segments)
        {
            ct.ThrowIfCancellationRequested();
            await semaphore.WaitAsync(ct).ConfigureAwait(false);

            var local = segment;
            var t = Task.Run(async () =>
            {
                try
                {
                    var fileName = $"{local.Index:D5}.{extension}";
                    var filePath = Path.Combine(tempDir, fileName);

                    for (int attempt = 0; attempt < retryCount; attempt++)
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            var data = await DownloadDataAsync(local.Url, ct).ConfigureAwait(false);

                            if (decryptor is not null && !decryptor.NeedsFileBasedDecryption)
                            {
                                data = decryptor.DecryptSegment(data, local.Index, initData);
                            }

                            await File.WriteAllBytesAsync(filePath, data, ct).ConfigureAwait(false);

                            // CENC 等文件级解密：落盘后用 mp4decrypt / shaka 原地处理
                            if (decryptor is not null && decryptor.NeedsFileBasedDecryption)
                            {
                                decryptor.DecryptFile(filePath);
                            }

                            lock (lockObj)
                            {
                                results.Add((local.Index, filePath, initData));
                                Interlocked.Increment(ref completed);
                                Interlocked.Add(ref totalBytes, data.Length);
                                var cur = completed;
                                if (cur % 50 == 0 || cur == total)
                                {
                                    progress(cur, total, totalBytes);
                                }
                            }
                            return;
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex)
                        {
                            if (attempt < retryCount - 1)
                            {
                                await Task.Delay(500 * (1 << attempt), ct).ConfigureAwait(false);
                                _logger.Log($"[Retry {attempt + 1}/{retryCount}] seg #{local.Index}: {ex.Message}");
                            }
                            else
                            {
                                lock (lockObj) failed.Add(local.Index);
                            }
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct);
            tasks.Add(t);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        progress(completed, total, totalBytes);

        if (failed.Count > 0)
            throw new Exception($"下载失败的分片: {string.Join(", ", failed)}");

        return results.OrderBy(r => r.Index).ToList();
    }

    /// <summary>下载单个分片原始字节（含限速、重定向跟随）</summary>
    public async Task<byte[]> DownloadDataAsync(string url, CancellationToken ct)
    {
        using var resp = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        // 流式读取以便限速
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var ms = new MemoryStream();
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            if (_throttle is not null)
                await _throttle.WaitForBytesAsync(read, ct).ConfigureAwait(false);
            ms.Write(buffer, 0, read);
        }
        return ms.ToArray();
    }

    /// <summary>
    /// 应用 --custom-range 过滤分片。
    /// 支持: "0-10"(序号闭区间) / "10-"(从序号10起) / "-99"(前100个) / "05:00-20:00"(时间区间)。
    /// </summary>
    public static List<MediaSegment> ApplyCustomRange(List<MediaSegment> segments, string? range)
    {
        if (string.IsNullOrWhiteSpace(range)) return segments;
        var r = range.Trim();

        // 时间格式 mm:ss-mm:ss 或 hh:mm:ss-hh:mm:ss
        if (r.Contains(':'))
        {
            var parts = r.Split('-', StringSplitOptions.TrimEntries);
            if (parts.Length != 2) return segments;
            var startSec = ParseTime(parts[0]);
            var endSec = ParseTime(parts[1]);
            if (startSec < 0 || endSec < 0) return segments;
            return segments.Where(s =>
                s.DurationSeconds.HasValue &&
                CumulativeStart(s, segments) >= startSec &&
                CumulativeStart(s, segments) <= endSec).ToList();
        }

        // 序号格式
        if (r.StartsWith('-'))
        {
            if (int.TryParse(r[1..], out var n)) return segments.Take(n).ToList();
        }
        else if (r.EndsWith('-'))
        {
            if (int.TryParse(r[..^1], out var start)) return segments.Where(s => s.Index >= start).ToList();
        }
        else
        {
            var parts = r.Split('-', StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out var s0) && int.TryParse(parts[1], out var s1))
                return segments.Where(seg => seg.Index >= s0 && seg.Index <= s1).ToList();
        }
        return segments;
    }

    private static double CumulativeStart(MediaSegment seg, List<MediaSegment> all)
    {
        double t = 0;
        foreach (var s in all)
        {
            if (s.Index == seg.Index) break;
            t += s.DurationSeconds ?? 0;
        }
        return t;
    }

    private static double ParseTime(string s)
    {
        var parts = s.Split(':', StringSplitOptions.TrimEntries);
        double h = 0, m = 0, sec = 0;
        if (parts.Length == 3) { double.TryParse(parts[0], out h); double.TryParse(parts[1], out m); double.TryParse(parts[2], out sec); }
        else if (parts.Length == 2) { double.TryParse(parts[0], out m); double.TryParse(parts[1], out sec); }
        else if (parts.Length == 1) { double.TryParse(parts[0], out sec); }
        return h * 3600 + m * 60 + sec;
    }
}

public delegate void ProgressCallback(int completed, int total, long bytes);

public interface IDecryptionProvider
{
    /// <summary>是否需要文件级解密（如 CENC 调用 mp4decrypt）</summary>
    bool NeedsFileBasedDecryption { get; }

    /// <summary>内存解密单个分片</summary>
    byte[] DecryptSegment(byte[] encrypted, int segmentIndex, byte[]? initData);

    /// <summary>文件级原地解密（CENC）</summary>
    void DecryptFile(string filePath);
}
