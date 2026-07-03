using System.Collections.ObjectModel;
using LYBox.Plugin.Downloader.Models;
using LYBox.Plugin.Downloader.Resources;
using LYBox.Plugin.Downloader.Services;
using LYBox.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LYBox.Plugin.Downloader.ViewModels;

/// <summary>
/// 直播录制页 ViewModel。对应 N_m3u8DL-RE 的直播录制能力。
/// 映射 CLI 参数：--live-record-limit、--live-wait-time、--live-take-count、
/// --live-real-time-merge、--live-keep-segments、--live-pipe-mux、
/// --live-fix-vtt-by-audio、--live-perform-as-vod。
/// </summary>
[NavigationItem("Downloader_Live")]
[Menu("NAV_Downloader_Live", "Downloader_Live", ParentKey = "NAV_Downloader", Order = 2)]
[ViewMap(typeof(Pages.LiveRecordPage))]
public partial class LiveRecordViewModel : DownloaderViewModelBase
{
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string _savePath = string.Empty;
    [ObservableProperty] private string _saveName = string.Empty;

    // 直播专用
    [ObservableProperty] private string _liveRecordLimit = string.Empty;   // HH:mm:ss
    [ObservableProperty] private int _liveWaitTime = 5;                    // 秒
    [ObservableProperty] private int _liveTakeCount = 16;
    [ObservableProperty] private bool _liveRealTimeMerge = true;
    [ObservableProperty] private bool _liveKeepSegments;
    [ObservableProperty] private bool _livePipeMux;
    [ObservableProperty] private bool _liveFixVttByAudio;
    [ObservableProperty] private bool _livePerformAsVod;

    // 通用下载控制
    [ObservableProperty] private int _threadCount = 8;
    [ObservableProperty] private int _retryCount = 3;
    [ObservableProperty] private string _headers = string.Empty;

    // 解密（直播可能加密）
    [ObservableProperty] private ObservableCollection<DecryptionKeyEntry> _decryptionKeys = [];
    [ObservableProperty] private DecryptionEngine _decryptionEngine = DecryptionEngine.Mp4Decrypt;
    [ObservableProperty] private string _customHlsKey = string.Empty;

    protected override string GetDisplayName() => Strings.Get("NAV_Downloader_Live");

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
        if (string.IsNullOrWhiteSpace(Url))
        {
            AddLogEntry(new LogEntry { Message = Strings.Get("MSG_NoValidM3u8Url") });
            return;
        }

        var keys = DecryptionKeys
            .Where(k => !string.IsNullOrWhiteSpace(k.Key))
            .Select(k => (Kid: string.IsNullOrWhiteSpace(k.Kid) ? null : k.Kid, KeyBytes: ParseKeyBytes(k.Key)))
            .Where(k => k.KeyBytes is not null)
            .Select(k => new DecryptionKey(k.Kid, k.KeyBytes!))
            .ToList();

        var opts = new DownloadOptions
        {
            Input = Url,
            SaveDir = SavePath,
            SaveName = string.IsNullOrWhiteSpace(SaveName)
                ? $"live_{DateTime.Now:yyyyMMdd_HHmmss}"
                : SaveName,
            ThreadCount = ThreadCount,
            RetryCount = RetryCount,
            Headers = ParseHeaders(Headers),
            Keys = keys,
            DecryptionEngine = DecryptionEngine,
            CustomHlsKey = string.IsNullOrWhiteSpace(CustomHlsKey) ? null : CustomHlsKey,

            // 直播参数
            LivePerformAsVod = LivePerformAsVod,
            LiveRealTimeMerge = LiveRealTimeMerge,
            LiveKeepSegments = LiveKeepSegments,
            LivePipeMux = LivePipeMux,
            LiveFixVttByAudio = LiveFixVttByAudio,
            LiveRecordLimit = ParseTimeSpan(LiveRecordLimit),
            LiveWaitTime = LiveWaitTime > 0 ? LiveWaitTime : null,
            LiveTakeCount = LiveTakeCount > 0 ? LiveTakeCount : 16
        };
        ApplyBinaryConfig(opts);

        var logger = CreateUiLogger();
        var orchestrator = new DownloadOrchestrator(logger, opts);
        await orchestrator.ExecuteAsync(ct);
    }

    private static TimeSpan? ParseTimeSpan(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var parts = s.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int h = 0, m = 0, sec = 0;
        if (parts.Length == 3) { int.TryParse(parts[0], out h); int.TryParse(parts[1], out m); int.TryParse(parts[2], out sec); }
        else if (parts.Length == 2) { int.TryParse(parts[0], out m); int.TryParse(parts[1], out sec); }
        else if (parts.Length == 1) { int.TryParse(parts[0], out sec); }
        var ts = new TimeSpan(h, m, sec);
        return ts > TimeSpan.Zero ? ts : null;
    }

    private static Dictionary<string, string> ParseHeaders(string text)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return dict;
        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var sep = line.IndexOf(':');
            if (sep < 0) sep = line.IndexOf('=');
            if (sep <= 0) continue;
            dict[line[..sep].Trim()] = line[(sep + 1)..].Trim();
        }
        return dict;
    }

    private static byte[]? ParseKeyBytes(string s)
    {
        try { return Convert.FromHexString(s.Trim()); }
        catch { return null; }
    }
}
