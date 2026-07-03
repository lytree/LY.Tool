using LYBox.Plugin.Downloader.Models;
using LYBox.Plugin.Downloader.Resources;
using LYBox.Plugin.Downloader.Services;
using LYBox.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LYBox.Plugin.Downloader.ViewModels;

/// <summary>
/// 工具设置页 ViewModel。配置 N_m3u8DL-RE 功能所依赖的外部二进制路径
/// （ffmpeg / mp4decrypt / mkvmerge / shaka-packager）、代理与日志级别。
/// 持久化到 %LOCALAPPDATA%/LYBox/DownloaderPlugin/settings.json。
/// 不涉及下载执行；"执行"按钮在此页语义为"探测二进制可用性"。
/// </summary>
[NavigationItem("Downloader_Settings")]
[Menu("NAV_Downloader_Settings", "Downloader_Settings", ParentKey = "NAV_Downloader", Order = 4)]
[ViewMap(typeof(Pages.ToolSettingsPage))]
public partial class ToolSettingsViewModel : DownloaderViewModelBase
{
    [ObservableProperty] private string _ffmpegPath = string.Empty;
    [ObservableProperty] private string _mp4DecryptPath = string.Empty;
    [ObservableProperty] private string _mkvmergePath = string.Empty;
    [ObservableProperty] private string _shakaPackagerPath = string.Empty;
    [ObservableProperty] private string _proxy = string.Empty;
    [ObservableProperty] private bool _useSystemProxy = true;
    [ObservableProperty] private string _logLevel = "INFO";

    protected override string GetDisplayName() => Strings.Get("NAV_Downloader_Settings");

    public ToolSettingsViewModel()
    {
        // 从持久化配置加载到可编辑字段
        var cfg = DownloadSettingsStore.Current;
        FfmpegPath = cfg.FfmpegPath;
        Mp4DecryptPath = cfg.Mp4DecryptPath;
        MkvmergePath = cfg.MkvmergePath;
        ShakaPackagerPath = cfg.ShakaPackagerPath;
        Proxy = cfg.Proxy ?? string.Empty;
        UseSystemProxy = cfg.UseSystemProxy;
        LogLevel = cfg.LogLevel;
    }

    [RelayCommand]
    private async Task BrowseFfmpeg()
    {
        var p = await BrowseFileAsync(Strings.Get("DIALOG_SelectFfmpeg"), "ffmpeg.exe", "ffmpeg");
        if (p is not null) FfmpegPath = p;
    }

    [RelayCommand]
    private async Task BrowseMp4Decrypt()
    {
        var p = await BrowseFileAsync(Strings.Get("DIALOG_SelectMp4Decrypt"), "mp4decrypt.exe", "mp4decrypt");
        if (p is not null) Mp4DecryptPath = p;
    }

    [RelayCommand]
    private async Task BrowseMkvmerge()
    {
        var p = await BrowseFileAsync(Strings.Get("DIALOG_SelectMkvmerge"), "mkvmerge.exe", "mkvmerge");
        if (p is not null) MkvmergePath = p;
    }

    [RelayCommand]
    private async Task BrowseShaka()
    {
        var p = await BrowseFileAsync(Strings.Get("DIALOG_SelectShaka"), "shaka-packager.exe", "shaka-packager");
        if (p is not null) ShakaPackagerPath = p;
    }

    /// <summary>保存设置到 JSON</summary>
    [RelayCommand]
    private void Save()
    {
        var cfg = new BinaryPaths
        {
            FfmpegPath = FfmpegPath,
            Mp4DecryptPath = Mp4DecryptPath,
            MkvmergePath = MkvmergePath,
            ShakaPackagerPath = ShakaPackagerPath,
            Proxy = string.IsNullOrWhiteSpace(Proxy) ? null : Proxy,
            UseSystemProxy = UseSystemProxy,
            LogLevel = LogLevel
        };
        DownloadSettingsStore.Save(cfg);
        AddLogEntry(new LogEntry { Message = Strings.Get("MSG_SettingsSaved") });
    }

    /// <summary>"执行"按钮在此页 = 探测所有二进制可用性</summary>
    protected override async Task ExecuteCoreAsync(CancellationToken ct)
    {
        var cfg = new BinaryPaths
        {
            FfmpegPath = FfmpegPath,
            Mp4DecryptPath = Mp4DecryptPath,
            MkvmergePath = MkvmergePath,
            ShakaPackagerPath = ShakaPackagerPath
        };

        AddLogEntry(new LogEntry { Message = Strings.Get("LOG_ProbingBinaries") });

        await ProbeAsync(BinaryLocator.ResolveFfmpeg(cfg), "ffmpeg", ct);
        await ProbeAsync(BinaryLocator.ResolveMkvmerge(cfg), "mkvmerge", ct);
        await ProbeAsync(BinaryLocator.ResolveMp4Decrypt(cfg), "mp4decrypt", ct);
        await ProbeAsync(BinaryLocator.ResolveShaka(cfg), "shaka-packager", ct);

        AddLogEntry(new LogEntry { Message = Strings.Get("LOG_ProbeComplete") });
    }

    private async Task ProbeAsync(string? path, string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            AddLogEntry(new LogEntry { Message = Strings.Get("FMT_BinaryNotFound", name) });
            return;
        }
        var version = await BinaryLocator.ProbeVersionAsync(path, ct);
        AddLogEntry(new LogEntry
        {
            Message = version is not null
                ? Strings.Get("FMT_BinaryOk", name, path)
                : Strings.Get("FMT_BinaryFailed", name, path)
        });
    }
}
