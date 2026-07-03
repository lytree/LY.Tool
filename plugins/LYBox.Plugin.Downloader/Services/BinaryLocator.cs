using System.Diagnostics;
using LYBox.Plugin.Downloader.Models;

namespace LYBox.Plugin.Downloader.Services;

/// <summary>
/// 外部二进制发现工具：优先使用用户在设置页配置的路径；
/// 未配置时在 PATH 中搜索 ffmpeg / mp4decrypt / mkvmerge / shaka-packager。
/// 对应 N_m3u8DL-RE --ffmpeg-binary-path / --decryption-binary-path 等参数的解析。
/// </summary>
public static class BinaryLocator
{
    private static readonly string[] WindowsExts = [".exe", ".cmd", ".bat", ""];

    /// <summary>返回有效 ffmpeg 路径；找不到返回 "ffmpeg"（交由调用方报错）</summary>
    public static string ResolveFfmpeg(BinaryPaths cfg)
        => Resolve(cfg.EffectiveFfmpeg(), "ffmpeg");

    public static string ResolveMkvmerge(BinaryPaths cfg)
        => Resolve(cfg.EffectiveMkvmerge(), "mkvmerge");

    public static string? ResolveMp4Decrypt(BinaryPaths cfg)
        => cfg.EffectiveMp4Decrypt() is { } p && File.Exists(p) ? p : FindInPath("mp4decrypt");

    public static string? ResolveShaka(BinaryPaths cfg)
        => cfg.EffectiveShaka() is { } p && File.Exists(p) ? p : FindInPath("shaka-packager");

    private static string Resolve(string configured, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (File.Exists(configured)) return configured;
            // 当作 PATH 中的命令名
            var found = FindInPath(Path.GetFileNameWithoutExtension(configured));
            if (found is not null) return found;
            return configured; // 返回原值，调用时由进程报错
        }
        return FindInPath(fallback) ?? fallback;
    }

    /// <summary>在 PATH 中查找命令的全路径；找不到返回 null</summary>
    public static string? FindInPath(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return null;

        var exts = OperatingSystem.IsWindows() ? WindowsExts : [""];
        var separator = OperatingSystem.IsWindows() ? ';' : ':';

        foreach (var dir in pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            foreach (var ext in exts)
            {
                try
                {
                    var candidate = Path.Combine(dir, command + ext);
                    if (File.Exists(candidate))
                        return Path.GetFullPath(candidate);
                }
                catch { /* 忽略非法路径 */ }
            }
        }
        return null;
    }

    /// <summary>探测 ffmpeg 版本，返回版本字符串或 null</summary>
    public static async Task<string?> ProbeVersionAsync(string binaryPath, CancellationToken ct = default)
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            var result = await CliWrap.Cli.Wrap(binaryPath)
                .WithArguments("-version")
                .WithStandardOutputPipe(CliWrap.PipeTarget.ToStringBuilder(sb))
                .ExecuteAsync(ct);
            return result.ExitCode == 0 ? sb.ToString().Split('\n').FirstOrDefault()?.Trim() : null;
        }
        catch
        {
            return null;
        }
    }
}
