using System.Text;
using CliWrap;
using LYBox.Plugin.Downloader.Models;

namespace LYBox.Plugin.Downloader.Services;

/// <summary>
/// 分片合并服务：对应 N_m3u8DL-RE 的 --binary-merge、--use-ffmpeg-concat-demuxer、--skip-merge。
/// - 二进制合并：直接拼接分片字节（适用于 .ts）
/// - ffmpeg concat 协议：`ffmpeg -i "concat:a.ts|b.ts|..." -c copy out`
/// - ffmpeg concat demuxer：`ffmpeg -f concat -safe 0 -i list.txt -c copy out`（默认）
/// </summary>
public class MergeService
{
    private readonly DirectUiLogger _logger;
    private readonly string _ffmpegPath;

    public MergeService(DirectUiLogger logger, string ffmpegPath)
    {
        _logger = logger;
        _ffmpegPath = ffmpegPath;
    }

    public async Task MergeAsync(
        List<string> segmentFiles,
        string outputPath,
        bool binaryMerge,
        bool useConcatDemuxer,
        bool skipMerge,
        CancellationToken ct,
        string? initFile = null)
    {
        if (skipMerge)
        {
            _logger.Log("已跳过合并 (--skip-merge)");
            return;
        }

        if (segmentFiles.Count == 0)
            throw new InvalidOperationException("没有可合并的分片");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");

        // fMP4 流（DASH / fMP4-HLS，含 EXT-X-MAP 或 MPD init 段）：
        // 媒体分片是 moof/mdat fragment，缺 moov，ffmpeg concat demuxer 无法解析（会崩溃），
        // 必须采用「init 段 + 分片顺序拼接」的二进制合并。
        if (initFile is not null)
        {
            await BinaryMergeAsync(segmentFiles, outputPath, ct, initFile);
            return;
        }

        if (binaryMerge)
        {
            await BinaryMergeAsync(segmentFiles, outputPath, ct);
            return;
        }

        if (useConcatDemuxer || segmentFiles.Count > 50)
        {
            await FfmpegConcatDemuxerAsync(segmentFiles, outputPath, ct);
            return;
        }

        await FfmpegConcatProtocolAsync(segmentFiles, outputPath, ct);
    }

    /// <summary>二进制合并：直接拼接字节流（fMP4 时 init 段前置）</summary>
    private async Task BinaryMergeAsync(List<string> segments, string output, CancellationToken ct, string? initFile = null)
    {
        _logger.Log(initFile is null
            ? "使用二进制合并 (--binary-merge)"
            : "fMP4 流: init 段 + 分片顺序二进制拼接");
        await using var outStream = new FileStream(output, FileMode.Create, FileAccess.Write, FileShare.None);

        // fMP4 必须先写 init 段（含 moov），否则后续 moof/mdat 无法解析
        if (initFile is not null)
        {
            await using var initStream = File.OpenRead(initFile);
            await initStream.CopyToAsync(outStream, ct);
        }

        foreach (var f in segments)
        {
            await using var inStream = File.OpenRead(f);
            await inStream.CopyToAsync(outStream, ct);
        }
        _logger.Log("二进制合并完成");
    }

    /// <summary>ffmpeg concat demuxer（写 list.txt，适合大量分片与各种格式）</summary>
    private async Task FfmpegConcatDemuxerAsync(List<string> segments, string output, CancellationToken ct)
    {
        var tempDir = Path.GetDirectoryName(segments[0]) ?? ".";
        var listPath = Path.Combine(tempDir, "filelist.txt");
        await WriteConcatListAsync(segments, listPath, ct);

        _logger.Log("使用 ffmpeg concat demuxer 合并...");
        var args = $"-y -f concat -safe 0 -i \"{listPath}\" -c copy \"{output}\"";
        await RunFfmpegAsync(args, ct);
    }

    /// <summary>ffmpeg concat 协议（拼接路径，仅适合 TS/MPEG-PS）</summary>
    private async Task FfmpegConcatProtocolAsync(List<string> segments, string output, CancellationToken ct)
    {
        _logger.Log("使用 ffmpeg concat 协议合并...");
        var concat = string.Join("|", segments.Select(Uri.EscapeDataString));
        // concat 协议不支持空格，使用单引号包裹
        var parts = string.Join("|", segments.Select(s => s.Replace("'", @"'\''")));
        var args = $"-y -i \"concat:{parts}\" -c copy \"{output}\"";
        await RunFfmpegAsync(args, ct);
    }

    private async Task WriteConcatListAsync(List<string> segments, string listPath, CancellationToken ct)
    {
        using var writer = new StreamWriter(listPath, false, new UTF8Encoding(false));
        foreach (var f in segments)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync($"file '{f.Replace("'", @"'\''")}'");
        }
    }

    private async Task RunFfmpegAsync(string args, CancellationToken ct)
    {
        var result = await Cli.Wrap(_ffmpegPath).WithArguments(args)
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
            {
                if (!string.IsNullOrEmpty(line)) _logger.Log($"[FFmpeg] {line}");
            }))
            .ExecuteAsync(ct);
        if (result.ExitCode != 0)
            throw new Exception($"FFmpeg 退出码 {result.ExitCode}");
        _logger.Log("合并完成");
    }
}
