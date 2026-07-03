namespace LYBox.Plugin.Downloader.Models;

/// <summary>
/// 外部二进制路径与全局设置（对应设置页）。
/// N_m3u8DL-RE 本身不依赖；ffmpeg / mp4decrypt / mkvmerge / shaka-packager 为外部工具。
/// 持久化为 JSON（见 DownloadSettingsStore）。
/// </summary>
public class BinaryPaths
{
    public string FfmpegPath { get; set; } = string.Empty;
    public string Mp4DecryptPath { get; set; } = string.Empty;
    public string MkvmergePath { get; set; } = string.Empty;
    public string ShakaPackagerPath { get; set; } = string.Empty;

    public string? Proxy { get; set; }
    public bool UseSystemProxy { get; set; } = true;
    public string LogLevel { get; set; } = "INFO";

    /// <summary>解析为有效的 ffmpeg 路径（空则回退到 PATH 中的 "ffmpeg"）</summary>
    public string EffectiveFfmpeg() => Resolve(FfmpegPath, "ffmpeg");
    public string EffectiveMkvmerge() => Resolve(MkvmergePath, "mkvmerge");
    public string? EffectiveMp4Decrypt() => string.IsNullOrWhiteSpace(Mp4DecryptPath) ? null : Mp4DecryptPath;
    public string? EffectiveShaka() => string.IsNullOrWhiteSpace(ShakaPackagerPath) ? null : ShakaPackagerPath;

    private static string Resolve(string configured, string fallback)
        => string.IsNullOrWhiteSpace(configured) ? fallback : configured;
}
