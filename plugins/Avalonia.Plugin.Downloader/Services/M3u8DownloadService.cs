using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Plugin.Downloader.Models;
using CliWrap;

namespace Avalonia.Plugin.Downloader.Services;

public class M3u8DownloadService
{
    private readonly DirectUiLogger _logger;

    public M3u8DownloadService(DirectUiLogger logger)
    {
        _logger = logger;
    }

    public async Task DownloadAsync(
        string url,
        string output,
        int concurrency = 8,
        string quality = "best",
        Dictionary<string, string>? headers = null,
        string ffmpegPath = "ffmpeg",
        int retryCount = 3,
        CancellationToken ct = default)
    {
        var outputDir = Path.GetDirectoryName(Path.GetFullPath(output));
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var tempDir = Path.Combine(string.IsNullOrEmpty(outputDir) ? "." : outputDir, $".tmp_{timestamp}");

        try
        {
            Directory.CreateDirectory(tempDir);

            using var client = CreateHttpClient(headers);

            _logger.Log("正在获取 M3U8...");
            var masterContent = await FetchM3u8Async(client, url, ct);
            var streams = ParseMasterM3u8(masterContent, url);

            string targetUrl;
            M3u8Info? m3u8Info = null;

            if (streams.Count > 0)
            {
                var videoStreams = streams.Where(s => IsVideoStream(s.Codecs)).ToList();
                var displayStreams = videoStreams.Count > 0 ? videoStreams : streams;
                _logger.Log($"发现 {streams.Count} 个质量选项, {displayStreams.Count} 个视频流");
                foreach (var s in streams)
                {
                    var isVideo = IsVideoStream(s.Codecs);
                    var qualityLabel = GetQualityLabel(s.Resolution, s.Bandwidth);
                    var videoTag = isVideo ? "" : " (仅音频)";
                    _logger.Log($"  - {qualityLabel} ({FormatBandwidth(s.Bandwidth)}){videoTag}");
                }

                targetUrl = SelectStreamUrl(streams, quality, url);
                _logger.Log($"使用: {targetUrl}");

                var m3u8Content = await FetchM3u8Async(client, targetUrl, ct);
                m3u8Info = ParseM3u8(m3u8Content, targetUrl);
            }
            else
            {
                targetUrl = url;
                m3u8Info = ParseM3u8(masterContent, url);
            }

            _logger.Log($"发现 {m3u8Info.Segments.Count} 个分片");

            if (m3u8Info.KeyInfo != null)
            {
                _logger.Log($"加密方式: {m3u8Info.KeyInfo.Method}");
                if (m3u8Info.KeyInfo.Method is "AES-128" or "AES-128-ECB")
                {
                    var aesKey = await FetchAesKeyAsync(client, m3u8Info.KeyInfo, ct);
                    _logger.Log($"{m3u8Info.KeyInfo.Method} 密钥获取成功");
                    await DownloadSegmentsAsync(client, m3u8Info, tempDir, aesKey, m3u8Info.KeyInfo.Method, retryCount, concurrency, ct);
                }
                else if (m3u8Info.KeyInfo.Method == "SAMPLE-AES")
                {
                    throw new NotSupportedException("SAMPLE-AES (FairPlay) 加密需要许可证服务器。");
                }
                else if (m3u8Info.KeyInfo.Method == "CHACHA20")
                {
                    var key = await FetchAesKeyAsync(client, m3u8Info.KeyInfo, ct);
                    _logger.Log("CHACHA20 密钥获取成功");
                    await DownloadSegmentsAsync(client, m3u8Info, tempDir, key, m3u8Info.KeyInfo.Method, retryCount, concurrency, ct);
                }
                else
                {
                    throw new NotSupportedException($"不支持的加密方式 '{m3u8Info.KeyInfo.Method}'。");
                }
            }
            else
            {
                await DownloadSegmentsAsync(client, m3u8Info, tempDir, null, null, retryCount, concurrency, ct);
            }

            await WriteConcatListAsync(m3u8Info, tempDir, ct);
            await MergeWithFFmpegAsync(tempDir, output, ffmpegPath, ct);

            _logger.Log($"完成: {output}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Log($"错误: {ex.Message}");
            throw;
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    private HttpClient CreateHttpClient(Dictionary<string, string>? headers)
    {
        var handler = new HttpClientHandler();
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        if (headers != null)
        {
            foreach (var kv in headers)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(kv.Key, kv.Value);
            }
        }
        return client;
    }

    private async Task<string> FetchM3u8Async(HttpClient client, string url, CancellationToken ct)
    {
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private async Task<byte[]> FetchAesKeyAsync(HttpClient client, EncryptInfo keyInfo, CancellationToken ct)
    {
        var response = await client.GetAsync(keyInfo.KeyUrl, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    private string ResolveUrl(string baseUrl, string relativeUrl)
    {
        if (relativeUrl.StartsWith("http://") || relativeUrl.StartsWith("https://") || relativeUrl.StartsWith("file://"))
        {
            return relativeUrl;
        }

        var baseUri = new Uri(baseUrl);
        if (relativeUrl.StartsWith("/"))
        {
            return $"{baseUri.Scheme}://{baseUri.Authority}{relativeUrl}";
        }

        var basePath = baseUri.AbsolutePath;
        var lastSlash = basePath.LastIndexOf('/');
        if (lastSlash >= 0)
        {
            basePath = basePath[..(lastSlash + 1)];
        }
        return new Uri(baseUri, basePath + relativeUrl).ToString();
    }

    private List<StreamInfo> ParseMasterM3u8(string content, string m3u8Url)
    {
        var streams = new List<StreamInfo>();
        var lines = content.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.StartsWith("#EXT-X-STREAM-INF:"))
            {
                var bandwidthMatch = Regex.Match(line, @"BANDWIDTH=(\d+)");
                var resolutionMatch = Regex.Match(line, @"RESOLUTION=(\d+x\d+)");
                var codecsMatch = Regex.Match(line, @"CODECS=""([^""]+)""");

                if (bandwidthMatch.Success && i + 1 < lines.Length)
                {
                    var urlLine = lines[i + 1].Trim();
                    if (!string.IsNullOrEmpty(urlLine) && !urlLine.StartsWith("#"))
                    {
                        streams.Add(new StreamInfo(
                            Bandwidth: long.Parse(bandwidthMatch.Groups[1].Value),
                            Resolution: resolutionMatch.Success ? resolutionMatch.Groups[1].Value : "unknown",
                            Codecs: codecsMatch.Success ? codecsMatch.Groups[1].Value : "",
                            Url: ResolveUrl(m3u8Url, urlLine)
                        ));
                    }
                }
            }
        }

        return [.. streams.OrderByDescending(s => s.Bandwidth)];
    }

    private string SelectStreamUrl(List<StreamInfo> streams, string quality, string originalUrl)
    {
        var videoStreams = streams.Where(s => IsVideoStream(s.Codecs)).ToList();
        var validStreams = videoStreams.Count > 0 ? videoStreams : streams;

        if (quality == "best")
        {
            return validStreams[0].Url;
        }
        else if (quality == "worst")
        {
            return validStreams[^1].Url;
        }
        else if (long.TryParse(quality, out var bandwidth))
        {
            var matched = validStreams.FirstOrDefault(s => s.Bandwidth <= bandwidth);
            return matched?.Url ?? validStreams[0].Url;
        }

        var exactMatch = validStreams.FirstOrDefault(s => s.Resolution == quality);
        return exactMatch?.Url ?? validStreams[0].Url;
    }

    private bool IsVideoStream(string codecs)
    {
        if (string.IsNullOrEmpty(codecs)) return true;
        return codecs.Contains("avc") || codecs.Contains("hvc") || codecs.Contains("hev") || codecs.Contains("vp0") || codecs.Contains("av1");
    }

    private M3u8Info ParseM3u8(string content, string m3u8Url)
    {
        var segments = new List<TsSegment>();
        EncryptInfo? keyInfo = null;
        var lines = content.Split('\n');
        double? currentDuration = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.StartsWith("#EXT-X-KEY:"))
            {
                var methodMatch = Regex.Match(line, @"METHOD=([^,]+)");
                var uriMatch = Regex.Match(line, @"URI=""([^""]+)""");
                var ivMatch = Regex.Match(line, @"IV=0x([0-9a-fA-F]+)");

                var method = methodMatch.Success ? methodMatch.Groups[1].Value : "";
                var keyUrl = uriMatch.Success ? uriMatch.Groups[1].Value : "";
                var iv = ivMatch.Success ? ivMatch.Groups[1].Value : null;

                keyUrl = ResolveUrl(m3u8Url, keyUrl);
                keyInfo = new EncryptInfo(method, keyUrl, iv);
            }
            else if (line.StartsWith("#EXTINF:"))
            {
                var durationStr = line["#EXTINF:".Length..];
                var commaIdx = durationStr.IndexOf(',');
                if (commaIdx >= 0)
                {
                    durationStr = durationStr[..commaIdx];
                }
                if (double.TryParse(durationStr, out var dur))
                {
                    currentDuration = dur;
                }
            }
            else if (!string.IsNullOrEmpty(line) && !line.StartsWith("#"))
            {
                var resolvedUrl = ResolveUrl(m3u8Url, line);
                segments.Add(new TsSegment(segments.Count, resolvedUrl, currentDuration));
                currentDuration = null;
            }
        }

        return new M3u8Info(segments, keyInfo);
    }

    private async Task DownloadSegmentsAsync(HttpClient client, M3u8Info m3u8Info, string tempDir, byte[]? key, string? method, int retryCount, int concurrency, CancellationToken ct)
    {
        var failedSegments = new List<int>();
        var completed = 0;
        var totalBytes = 0L;
        var total = m3u8Info.Segments.Count;
        var startTime = DateTime.Now;
        var lockObj = new object();

        using var semaphore = new SemaphoreSlim(concurrency);
        var tasks = new List<Task>();

        foreach (var segment in m3u8Info.Segments)
        {
            ct.ThrowIfCancellationRequested();
            await semaphore.WaitAsync(ct);

            var localSegment = segment;
            var t = Task.Run(async () =>
            {
                try
                {
                    var fileName = $"{localSegment.Index:D5}.ts";
                    var filePath = Path.Combine(tempDir, fileName);

                    for (int attempt = 0; attempt < retryCount; attempt++)
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            var data = await DownloadSegmentAsync(client, localSegment.Url, ct);

                            if (key != null && method != null)
                            {
                                data = DecryptSegment(data, key, localSegment.Index, m3u8Info.KeyInfo?.Iv, method);
                            }

                            await File.WriteAllBytesAsync(filePath, data, ct);
                            Interlocked.Increment(ref completed);
                            Interlocked.Add(ref totalBytes, data.Length);

                            var currentCompleted = completed;
                            if (currentCompleted % 50 == 0 || currentCompleted == total)
                            {
                                _logger.Log($"下载进度: {currentCompleted}/{total} ({FormatSize(totalBytes)})");
                            }
                            return;
                        }
                        catch (OperationCanceledException) { throw; }
                        catch
                        {
                            if (attempt < retryCount - 1)
                                await Task.Delay(500 * (1 << attempt), ct);
                        }
                    }
                    lock (lockObj)
                    {
                        failedSegments.Add(localSegment.Index);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct);
            tasks.Add(t);
        }

        await Task.WhenAll(tasks);

        var elapsed = DateTime.Now - startTime;
        var speed = elapsed.TotalSeconds > 0 ? totalBytes / elapsed.TotalSeconds : 0;
        _logger.Log($"已下载 {completed}/{total} 个分片 ({FormatSize(totalBytes)}), 耗时 {elapsed.TotalSeconds:F1}s ({FormatSpeed(speed)})");

        if (failedSegments.Count > 0)
        {
            throw new Exception($"下载失败的分片: {string.Join(", ", failedSegments)}");
        }
    }

    private async Task<byte[]> DownloadSegmentAsync(HttpClient client, string url, CancellationToken ct)
    {
        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

        if (((int)response.StatusCode).ToString().StartsWith("30"))
        {
            if (response.Headers.Location != null)
            {
                var redirectedUrl = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location.AbsoluteUri
                    : new Uri(new Uri(url), response.Headers.Location).ToString();
                return await client.GetByteArrayAsync(redirectedUrl, ct);
            }
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    private byte[] DecryptSegment(byte[] encryptedData, byte[] key, int segmentIndex, string? ivHex, string method)
    {
        if (method == "AES-128-ECB")
        {
            return AESDecrypt(encryptedData, key, null, CipherMode.ECB);
        }

        byte[] iv;
        if (ivHex != null)
        {
            iv = new byte[16];
            var hexBytes = Convert.FromHexString(ivHex);
            var offset = 16 - hexBytes.Length;
            Array.Copy(hexBytes, 0, iv, offset, hexBytes.Length);
        }
        else
        {
            iv = new byte[16];
            var indexBytes = BitConverter.GetBytes(segmentIndex);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(indexBytes);
            }
            Array.Copy(indexBytes, 0, iv, 12, 4);
        }

        if (method == "CHACHA20")
        {
            return ChaCha20Decrypt(encryptedData, key, iv);
        }

        return AESDecrypt(encryptedData, key, iv, CipherMode.CBC);
    }

    private byte[] AESDecrypt(byte[] encryptedData, byte[] key, byte[]? iv, CipherMode mode)
    {
        var aes = Aes.Create();
        aes.BlockSize = 128;
        aes.KeySize = 128;
        aes.Key = key;
        aes.IV = iv ?? new byte[16];
        aes.Mode = mode;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        using var msInput = new MemoryStream(encryptedData);
        using var cs = new CryptoStream(msInput, decryptor, CryptoStreamMode.Read);
        using var msOutput = new MemoryStream();
        cs.CopyTo(msOutput);
        return msOutput.ToArray();
    }

    private byte[] ChaCha20Decrypt(byte[] ciphertext, byte[] key, byte[] nonce)
    {
        var decrypted = new byte[ciphertext.Length];

        var state = new uint[16];

        state[0] = 0x61707865;
        state[1] = 0x3320646e;
        state[2] = 0x79622d32;
        state[3] = 0x6b206574;

        for (int i = 0; i < 8; i++)
        {
            state[4 + i] = BitConverter.ToUInt32(key, i * 4);
        }

        for (int i = 0; i < 3; i++)
        {
            state[12 + i] = BitConverter.ToUInt32(nonce, i * 4);
        }

        state[15] = BitConverter.ToUInt32(nonce, 12);

        for (int i = 0; i < ciphertext.Length; i += 64)
        {
            var workingState = (uint[])state.Clone();

            for (int round = 0; round < 10; round += 2)
            {
                ChaCha20QuarterRound(workingState, 0, 4, 8, 12);
                ChaCha20QuarterRound(workingState, 1, 5, 9, 13);
                ChaCha20QuarterRound(workingState, 2, 6, 10, 14);
                ChaCha20QuarterRound(workingState, 3, 7, 11, 15);
                ChaCha20QuarterRound(workingState, 0, 5, 10, 15);
                ChaCha20QuarterRound(workingState, 1, 6, 11, 12);
                ChaCha20QuarterRound(workingState, 2, 7, 8, 13);
                ChaCha20QuarterRound(workingState, 3, 4, 9, 14);
            }

            for (int j = 0; j < 16; j++)
            {
                workingState[j] += state[j];
            }

            var block = new byte[64];
            for (int j = 0; j < 16; j++)
            {
                var bytes = BitConverter.GetBytes(workingState[j]);
                if (BitConverter.IsLittleEndian)
                {
                    block[j * 4] = bytes[0];
                    block[j * 4 + 1] = bytes[1];
                    block[j * 4 + 2] = bytes[2];
                    block[j * 4 + 3] = bytes[3];
                }
                else
                {
                    block[j * 4 + 3] = bytes[0];
                    block[j * 4 + 2] = bytes[1];
                    block[j * 4 + 1] = bytes[2];
                    block[j * 4] = bytes[3];
                }
            }

            var remaining = Math.Min(64, ciphertext.Length - i);
            for (int j = 0; j < remaining; j++)
            {
                decrypted[i + j] = (byte)(ciphertext[i + j] ^ block[j]);
            }

            state[12] = state[12] + 1;
            if (state[12] == 0) state[13] = state[13] + 1;
        }

        return decrypted;
    }

    private static void ChaCha20QuarterRound(uint[] state, int a, int b, int c, int d)
    {
        state[a] += state[b];
        state[d] ^= state[a];
        state[d] = RotateLeft(state[d], 16);
        state[c] += state[d];
        state[b] ^= state[c];
        state[b] = RotateLeft(state[b], 12);
        state[a] += state[b];
        state[d] ^= state[a];
        state[d] = RotateLeft(state[d], 8);
        state[c] += state[d];
        state[b] ^= state[c];
        state[b] = RotateLeft(state[b], 7);
    }

    private static uint RotateLeft(uint value, int bits)
    {
        return (value << bits) | (value >> (32 - bits));
    }

    private string FormatBandwidth(long bandwidth)
    {
        if (bandwidth >= 1_000_000)
            return $"{bandwidth / 1_000_000.0:F1} Mbps";
        if (bandwidth >= 1_000)
            return $"{bandwidth / 1_000.0:F0} Kbps";
        return $"{bandwidth} bps";
    }

    private string GetQualityLabel(string resolution, long bandwidth)
    {
        if (!string.IsNullOrEmpty(resolution) && resolution != "unknown")
        {
            var parts = resolution.Split('x');
            if (parts.Length == 2 && int.TryParse(parts[0], out var height))
            {
                var label = height switch
                {
                    >= 2160 => "4K",
                    >= 1440 => "1440p",
                    >= 1080 => "1080p",
                    >= 720 => "720p",
                    >= 480 => "480p",
                    >= 360 => "360p",
                    >= 240 => "240p",
                    _ => $"{height}p"
                };
                return $"{label} ({resolution})";
            }
            return resolution;
        }
        return FormatBandwidth(bandwidth);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1_000_000_000)
            return $"{bytes / 1_000_000_000.0:F2} GB";
        if (bytes >= 1_000_000)
            return $"{bytes / 1_000_000.0:F2} MB";
        if (bytes >= 1_000)
            return $"{bytes / 1_000.0:F2} KB";
        return $"{bytes} B";
    }

    private static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond >= 1_000_000_000)
            return $"{bytesPerSecond / 1_000_000_000:F2} GB/s";
        if (bytesPerSecond >= 1_000_000)
            return $"{bytesPerSecond / 1_000_000:F2} MB/s";
        if (bytesPerSecond >= 1_000)
            return $"{bytesPerSecond / 1_000:F2} KB/s";
        return $"{bytesPerSecond:F0} B/s";
    }

    private async Task WriteConcatListAsync(M3u8Info m3u8Info, string tempDir, CancellationToken ct)
    {
        var listPath = Path.Combine(tempDir, "filelist.txt");
        using var writer = new StreamWriter(listPath, false, new UTF8Encoding(false));
        foreach (var segment in m3u8Info.Segments)
        {
            ct.ThrowIfCancellationRequested();
            var filePath = Path.Combine(tempDir, $"{segment.Index:D5}.ts");
            await writer.WriteLineAsync($"file '{filePath}'");
        }
    }

    private async Task MergeWithFFmpegAsync(string tempDir, string outputPath, string ffmpegPath, CancellationToken ct)
    {
        var listPath = Path.Combine(tempDir, "filelist.txt");

        _logger.Log("正在使用 FFmpeg 合并...");

        var result = await Cli.Wrap(ffmpegPath)
            .WithArguments($"-y -f concat -safe 0 -i \"{listPath}\" -c copy \"{outputPath}\"")
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
            {
                if (!string.IsNullOrEmpty(line))
                {
                    _logger.Log($"[FFmpeg] {line}");
                }
            }))
            .ExecuteAsync(ct);

        if (result.ExitCode != 0)
        {
            throw new Exception($"FFmpeg 退出码 {result.ExitCode}");
        }

        _logger.Log("合并完成!");
    }
}
