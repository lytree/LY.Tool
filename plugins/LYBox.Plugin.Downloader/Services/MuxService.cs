using System.Text;
using CliWrap;
using LYBox.Plugin.Downloader.Models;

namespace LYBox.Plugin.Downloader.Services;

/// <summary>
/// 混流服务：对应 N_m3u8DL-RE 的 -M/--mux-after-done 与 --mux-import。
/// 将分离的音视频/字幕合并为 mp4 或 mkv 容器，可选 ffmpeg 或 mkvmerge 作为混流器。
/// </summary>
public class MuxService
{
    private readonly DirectUiLogger _logger;
    private readonly string _ffmpegPath;
    private readonly string _mkvmergePath;

    public MuxService(DirectUiLogger logger, string ffmpegPath, string mkvmergePath)
    {
        _logger = logger;
        _ffmpegPath = ffmpegPath;
        _mkvmergePath = mkvmergePath;
    }

    /// <summary>
    /// 执行混流。
    /// </summary>
    /// <param name="primaryInput">主输入文件（通常是合并后的视频或 TS）</param>
    /// <param name="additionalInputs">附加输入（音频/字幕文件）</param>
    /// <param name="outputWithoutExt">输出文件名（不含扩展名）</param>
    /// <param name="mux">混流配置</param>
    /// <param name="imports">外部引入媒体</param>
    public async Task<string> MuxAsync(
        string primaryInput,
        List<string> additionalInputs,
        string outputWithoutExt,
        MuxOptions mux,
        List<MuxImport> imports,
        CancellationToken ct)
    {
        var ext = mux.Format.Equals("mkv", StringComparison.OrdinalIgnoreCase) ? ".mkv" : ".mp4";
        var outputPath = outputWithoutExt + ext;

        var allInputs = new List<string> { primaryInput };
        allInputs.AddRange(additionalInputs.Where(File.Exists));
        foreach (var imp in imports.Where(i => File.Exists(i.Path)))
            allInputs.Add(imp.Path);

        _logger.Log($"开始混流 → {Path.GetFileName(outputPath)} ({mux.Muxer})");

        if (mux.Muxer.Equals("mkvmerge", StringComparison.OrdinalIgnoreCase))
            await MuxWithMkvmergeAsync(allInputs, outputPath, imports, ct);
        else
            await MuxWithFfmpegAsync(allInputs, outputPath, mux, ct);

        _logger.Log($"混流完成: {outputPath}");

        // keep=false 时删除原始分离文件
        if (!mux.Keep)
        {
            foreach (var f in allInputs.Where(File.Exists))
            {
                try { File.Delete(f); } catch { /* 忽略 */ }
            }
        }
        return outputPath;
    }

    private async Task MuxWithFfmpegAsync(List<string> inputs, string output, MuxOptions mux, CancellationToken ct)
    {
        var sb = new StringBuilder("-y");
        foreach (var f in inputs) sb.Append($" -i \"{f}\"");
        // 映射所有输入的所有流；skip_sub 时排除字幕
        if (mux.SkipSub)
            sb.Append(" -map 0:v -map 0:a?");
        else
            sb.Append(" -map 0 -map 1? -map 2?");
        sb.Append(" -c copy");
        if (mux.Format.Equals("mp4", StringComparison.OrdinalIgnoreCase))
            sb.Append(" -movflags +faststart");
        sb.Append($" \"{output}\"");

        var result = await Cli.Wrap(mux.BinPath ?? _ffmpegPath).WithArguments(sb.ToString())
            .WithStandardErrorPipe(PipeTarget.ToDelegate(l => { if (!string.IsNullOrEmpty(l)) _logger.Log($"[FFmpeg] {l}"); }))
            .ExecuteAsync(ct);
        if (result.ExitCode != 0) throw new Exception($"FFmpeg 混流退出码 {result.ExitCode}");
    }

    private async Task MuxWithMkvmergeAsync(List<string> inputs, string output, List<MuxImport> imports, CancellationToken ct)
    {
        var sb = new StringBuilder($"-o \"{output}\"");
        // 第一个输入为视频
        sb.Append($" \"{inputs[0]}\"");
        // 后续输入为音频/字幕
        for (int i = 1; i < inputs.Count; i++)
        {
            var lang = imports.Count >= i ? imports[i - 1].Lang : null;
            sb.Append(lang is not null ? $" --language 0:{lang}" : "");
            sb.Append($" \"{inputs[i]}\"");
        }

        var result = await Cli.Wrap(_mkvmergePath).WithArguments(sb.ToString())
            .WithStandardOutputPipe(PipeTarget.ToDelegate(l => { if (!string.IsNullOrEmpty(l)) _logger.Log($"[mkvmerge] {l}"); }))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(l => { if (!string.IsNullOrEmpty(l)) _logger.Log($"[mkvmerge] {l}"); }))
            .ExecuteAsync(ct);
        // mkvmerge 成功退出码为 0 或 1（1=有警告但成功）
        if (result.ExitCode != 0 && result.ExitCode != 1)
            throw new Exception($"mkvmerge 退出码 {result.ExitCode}");
    }
}
