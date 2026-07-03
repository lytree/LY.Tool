using System.Collections.ObjectModel;
using LYBox.Plugin.Downloader.Models;
using LYBox.Plugin.Downloader.Resources;
using LYBox.Plugin.Downloader.Services;
using LYBox.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LYBox.Plugin.Downloader.ViewModels;

/// <summary>
/// 解密与混流页 ViewModel。独立工具页：
/// 1) 输入为本地加密 m3u8/MPD 播放清单 → 走下载编排器（解密分片 + 合并 + 混流）
/// 2) 输入为本地媒体文件 → 直接用 MuxService 与 --mux-import 引入的媒体混流为 mp4/mkv
/// 对应 N_m3u8DL-RE 的 --key/--decryption-engine/--custom-hls-* 与 -M/--mux-import。
/// </summary>
[NavigationItem("Downloader_DecryptMux")]
[Menu("NAV_Downloader_DecryptMux", "Downloader_DecryptMux", ParentKey = "NAV_Downloader", Order = 3)]
[ViewMap(typeof(Pages.DecryptMuxPage))]
public partial class DecryptMuxViewModel : DownloaderViewModelBase
{
    [ObservableProperty] private string _input = string.Empty;
    [ObservableProperty] private string _savePath = string.Empty;
    [ObservableProperty] private string _saveName = string.Empty;

    // 解密
    [ObservableProperty] private ObservableCollection<DecryptionKeyEntry> _decryptionKeys = [];
    [ObservableProperty] private DecryptionEngine _decryptionEngine = DecryptionEngine.Mp4Decrypt;
    [ObservableProperty] private string _customHlsMethod = string.Empty;
    [ObservableProperty] private string _customHlsKey = string.Empty;
    [ObservableProperty] private string _customHlsIv = string.Empty;

    // 混流
    [ObservableProperty] private bool _muxEnabled = true;
    [ObservableProperty] private string _muxFormat = "mp4";
    [ObservableProperty] private string _muxer = "ffmpeg";
    [ObservableProperty] private bool _muxKeep;
    [ObservableProperty] private ObservableCollection<MuxImportEntry> _muxImports = [];

    protected override string GetDisplayName() => Strings.Get("NAV_Downloader_DecryptMux");

    [RelayCommand]
    private void AddKey() => DecryptionKeys.Add(new DecryptionKeyEntry());

    [RelayCommand]
    private void RemoveKey(DecryptionKeyEntry key) => DecryptionKeys.Remove(key);

    [RelayCommand]
    private void AddImport() => MuxImports.Add(new MuxImportEntry());

    [RelayCommand]
    private void RemoveImport(MuxImportEntry entry) => MuxImports.Remove(entry);

    [RelayCommand]
    private async Task BrowseInput()
    {
        var path = await BrowseFileAsync(Strings.Get("DIALOG_SelectInput"), "*.m3u8", "*.mpd", "*.mp4", "*.ts", "*.m4a", "*.mkv");
        if (path is not null) Input = path;
    }

    [RelayCommand]
    private async Task BrowseSavePath()
    {
        var path = await BrowseFolderAsync(Strings.Get("DIALOG_SelectSavePath"));
        if (path is not null) SavePath = path;
    }

    [RelayCommand]
    private async Task BrowseImport(MuxImportEntry entry)
    {
        var path = await BrowseFileAsync(Strings.Get("DIALOG_SelectImportFile"), "*");
        if (path is not null) entry.Path = path;
    }

    protected override async Task ExecuteCoreAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Input))
        {
            AddLogEntry(new LogEntry { Message = Strings.Get("MSG_NoInputFile") });
            return;
        }

        var saveDir = string.IsNullOrWhiteSpace(SavePath) ? Environment.CurrentDirectory : SavePath;
        var saveName = string.IsNullOrWhiteSpace(SaveName)
            ? Path.GetFileNameWithoutExtension(Input)
            : SaveName;

        var keys = DecryptionKeys
            .Where(k => !string.IsNullOrWhiteSpace(k.Key))
            .Select(k => (Kid: string.IsNullOrWhiteSpace(k.Kid) ? null : k.Kid, KeyBytes: ParseKeyBytes(k.Key)))
            .Where(k => k.KeyBytes is not null)
            .Select(k => new DecryptionKey(k.Kid, k.KeyBytes!))
            .ToList();

        // 判断输入是播放清单还是媒体文件
        var isPlaylist = await IsPlaylistAsync(Input, ct);

        if (isPlaylist)
        {
            await RunPlaylistAsync(saveDir, saveName, keys, ct);
        }
        else
        {
            await RunMuxOnlyAsync(saveDir, saveName, ct);
        }
    }

    private async Task<bool> IsPlaylistAsync(string input, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(input) && !Uri.TryCreate(input, UriKind.Absolute, out _)) return false;
            if (File.Exists(input))
            {
                var text = await File.ReadAllTextAsync(input, ct);
                return text.Contains("#EXTM3U", StringComparison.OrdinalIgnoreCase) ||
                       text.Contains("<MPD", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { /* 忽略 */ }
        return false;
    }

    private async Task RunPlaylistAsync(string saveDir, string saveName, List<DecryptionKey> keys, CancellationToken ct)
    {
        var opts = new DownloadOptions
        {
            Input = Input,
            SaveDir = saveDir,
            SaveName = saveName,
            ThreadCount = 8,
            RetryCount = 3,
            Keys = keys,
            DecryptionEngine = DecryptionEngine,
            CustomHlsMethod = EmptyToNull(CustomHlsMethod),
            CustomHlsKey = EmptyToNull(CustomHlsKey),
            CustomHlsIv = EmptyToNull(CustomHlsIv),
            DelAfterDone = true,
            WriteMetaJson = false,
            MuxAfterDone = MuxEnabled
                ? new MuxOptions { Format = MuxFormat, Muxer = Muxer, Keep = MuxKeep }
                : null,
            MuxImports = MuxImports
                .Where(i => !string.IsNullOrWhiteSpace(i.Path))
                .Select(i => new MuxImport { Path = i.Path, Lang = EmptyToNull(i.Lang), Name = EmptyToNull(i.Name) })
                .ToList()
        };
        ApplyBinaryConfig(opts);

        var logger = CreateUiLogger();
        var orchestrator = new DownloadOrchestrator(logger, opts);
        await orchestrator.ExecuteAsync(ct);
    }

    private async Task RunMuxOnlyAsync(string saveDir, string saveName, CancellationToken ct)
    {
        if (!MuxEnabled)
        {
            AddLogEntry(new LogEntry { Message = Strings.Get("MSG_MuxDisabled") });
            return;
        }
        if (!File.Exists(Input))
        {
            AddLogEntry(new LogEntry { Message = Strings.Get("MSG_InputNotFound") });
            return;
        }

        var imports = MuxImports
            .Where(i => !string.IsNullOrWhiteSpace(i.Path) && File.Exists(i.Path))
            .Select(i => new MuxImport { Path = i.Path, Lang = EmptyToNull(i.Lang), Name = EmptyToNull(i.Name) })
            .ToList();

        var cfg = BinaryConfig;
        var muxer = new MuxService(
            CreateUiLogger(),
            string.IsNullOrWhiteSpace(cfg.FfmpegPath) ? "ffmpeg" : cfg.FfmpegPath,
            string.IsNullOrWhiteSpace(cfg.MkvmergePath) ? "mkvmerge" : cfg.MkvmergePath);

        var muxOpts = new MuxOptions { Format = MuxFormat, Muxer = Muxer, Keep = MuxKeep };
        var outWithoutExt = Path.Combine(saveDir, saveName);
        await muxer.MuxAsync(Input, imports.Select(i => i.Path).ToList(), outWithoutExt, muxOpts, imports, ct);
    }

    private static byte[]? ParseKeyBytes(string s)
    {
        try { return Convert.FromHexString(s.Trim()); }
        catch { return null; }
    }

    private static string? EmptyToNull(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
