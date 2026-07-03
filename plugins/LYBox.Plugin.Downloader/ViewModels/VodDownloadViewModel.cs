using System.Collections.ObjectModel;
using LYBox.Plugin.Downloader.Models;
using LYBox.Plugin.Downloader.Resources;
using LYBox.Plugin.Downloader.Services;
using LYBox.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LYBox.Plugin.Downloader.ViewModels;

/// <summary>
/// 点播下载页 ViewModel。对应 N_m3u8DL-RE 的点播下载能力（HLS/DASH/MSS）。
/// 把 CLI 参数映射为界面设置项：URL 列表、保存设置、并发/重试/超时、Headers、限速、范围、
/// 选流(-sv/-sa/-ss)、字幕、合并选项、混流(-M)、解密(--key/--custom-hls-*)。
/// </summary>
[NavigationItem("Downloader_Vod")]
[Menu("NAV_Downloader_Vod", "Downloader_Vod", ParentKey = "NAV_Downloader", Order = 1)]
[ViewMap(typeof(Pages.VodDownloadPage))]
public partial class VodDownloadViewModel : DownloaderViewModelBase
{
    [ObservableProperty] private ObservableCollection<DownloadTask> _downloadTasks = [];
    [ObservableProperty] private string _savePath = string.Empty;
    [ObservableProperty] private string _saveName = string.Empty;

    // 下载控制
    [ObservableProperty] private int _threadCount = 8;
    [ObservableProperty] private int _retryCount = 3;
    [ObservableProperty] private int _httpRequestTimeout = 100;
    [ObservableProperty] private string _headers = string.Empty;
    [ObservableProperty] private string _maxSpeed = string.Empty;
    [ObservableProperty] private string _customRange = string.Empty;

    // 选流
    [ObservableProperty] private bool _autoSelect;
    [ObservableProperty] private string _selectVideo = string.Empty;
    [ObservableProperty] private string _selectAudio = string.Empty;
    [ObservableProperty] private string _selectSubtitle = string.Empty;

    // 字幕
    [ObservableProperty] private bool _subOnly;
    [ObservableProperty] private string _subFormat = "SRT";
    [ObservableProperty] private bool _autoSubtitleFix = true;

    // 合并
    [ObservableProperty] private bool _skipMerge;
    [ObservableProperty] private bool _binaryMerge;
    [ObservableProperty] private bool _useFfmpegConcatDemuxer;
    [ObservableProperty] private bool _delAfterDone = true;
    [ObservableProperty] private bool _writeMetaJson = true;

    // 混流
    [ObservableProperty] private bool _muxEnabled;
    [ObservableProperty] private string _muxFormat = "mp4";
    [ObservableProperty] private string _muxer = "ffmpeg";
    [ObservableProperty] private bool _muxKeep;

    // 解密
    [ObservableProperty] private ObservableCollection<DecryptionKeyEntry> _decryptionKeys = [];
    [ObservableProperty] private DecryptionEngine _decryptionEngine = DecryptionEngine.Mp4Decrypt;
    [ObservableProperty] private string _customHlsMethod = string.Empty;
    [ObservableProperty] private string _customHlsKey = string.Empty;
    [ObservableProperty] private string _customHlsIv = string.Empty;

    protected override string GetDisplayName() => Strings.Get("NAV_Downloader_Vod");

    public VodDownloadViewModel()
    {
        DownloadTasks.Add(new DownloadTask());
    }

    [RelayCommand]
    private void AddTask() => DownloadTasks.Add(new DownloadTask());

    [RelayCommand]
    private void RemoveTask(DownloadTask task) => DownloadTasks.Remove(task);

    [RelayCommand]
    private void AddKey() => DecryptionKeys.Add(new DecryptionKeyEntry());

    [RelayCommand]
    private void RemoveKey(DecryptionKeyEntry key) => DecryptionKeys.Remove(key);

    [RelayCommand]
    private async Task BrowseSavePath()
    {
        var path = await BrowseFolderAsync(Strings.Get("DIALOG_SelectSavePath"));
        if (path is not null) SavePath = path;
    }

    protected override async Task ExecuteCoreAsync(CancellationToken ct)
    {
        var validTasks = DownloadTasks.Where(t => !string.IsNullOrWhiteSpace(t.Url)).ToList();
        if (validTasks.Count == 0)
        {
            AddLogEntry(new LogEntry { Message = Strings.Get("MSG_NoValidM3u8Url") });
            return;
        }

        var headers = ParseHeaders(Headers);
        var keys = DecryptionKeys
            .Where(k => !string.IsNullOrWhiteSpace(k.Key))
            .Select(k => (Kid: string.IsNullOrWhiteSpace(k.Kid) ? null : k.Kid, KeyBytes: ParseKeyBytes(k.Key)))
            .Where(k => k.KeyBytes is not null)
            .Select(k => new DecryptionKey(k.Kid, k.KeyBytes!))
            .ToList();

        for (int i = 0; i < validTasks.Count; i++)
        {
            var task = validTasks[i];
            if (validTasks.Count > 1)
                AddLogEntry(new LogEntry { Message = Strings.Get("FMT_Downloading", i + 1, validTasks.Count, task.Url) });

            var opts = BuildOptions(task, headers, keys);
            var logger = CreateUiLogger();
            var orchestrator = new DownloadOrchestrator(logger, opts);
            await orchestrator.ExecuteAsync(ct);
        }
    }

    private DownloadOptions BuildOptions(DownloadTask task, Dictionary<string, string> headers, List<DecryptionKey> keys)
    {
        var opts = new DownloadOptions
        {
            Input = task.Url,
            SaveDir = SavePath,
            SaveName = string.IsNullOrWhiteSpace(SaveName) ? task.EffectiveFileName : SaveName,
            ThreadCount = ThreadCount,
            RetryCount = RetryCount,
            HttpRequestTimeout = HttpRequestTimeout,
            Headers = headers,
            MaxSpeed = string.IsNullOrWhiteSpace(MaxSpeed) ? null : MaxSpeed,
            CustomRange = string.IsNullOrWhiteSpace(CustomRange) ? null : CustomRange,
            AutoSelect = AutoSelect,
            SelectVideo = EmptyToNull(SelectVideo),
            SelectAudio = EmptyToNull(SelectAudio),
            SelectSubtitle = EmptyToNull(SelectSubtitle),
            SubOnly = SubOnly,
            SubFormat = SubFormat,
            AutoSubtitleFix = AutoSubtitleFix,
            SkipMerge = SkipMerge,
            BinaryMerge = BinaryMerge,
            UseFfmpegConcatDemuxer = UseFfmpegConcatDemuxer,
            DelAfterDone = DelAfterDone,
            WriteMetaJson = WriteMetaJson,
            Keys = keys,
            DecryptionEngine = DecryptionEngine,
            CustomHlsMethod = EmptyToNull(CustomHlsMethod),
            CustomHlsKey = EmptyToNull(CustomHlsKey),
            CustomHlsIv = EmptyToNull(CustomHlsIv),
            MuxAfterDone = MuxEnabled
                ? new MuxOptions { Format = MuxFormat, Muxer = Muxer, Keep = MuxKeep }
                : null
        };
        ApplyBinaryConfig(opts);
        return opts;
    }

    /// <summary>解析 Headers 文本（每行 "Key: Value" 或 "Key=Value"）</summary>
    private static Dictionary<string, string> ParseHeaders(string text)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return dict;
        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var sep = line.IndexOf(':');
            if (sep < 0) sep = line.IndexOf('=');
            if (sep <= 0) continue;
            var key = line[..sep].Trim();
            var val = line[(sep + 1)..].Trim();
            if (!string.IsNullOrEmpty(key)) dict[key] = val;
        }
        return dict;
    }

    private static byte[]? ParseKeyBytes(string s)
    {
        try { return Convert.FromHexString(s.Trim()); }
        catch { return null; }
    }

    private static string? EmptyToNull(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
