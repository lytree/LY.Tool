using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.Downloader.Models;

/// <summary>
/// 点播下载页的一条 URL 任务条目（取代旧的 M3u8UrlEntry）。
/// 支持 HLS / DASH / MSS 任意输入链接，并可指定保存文件名。
/// </summary>
public partial class DownloadTask : ObservableObject
{
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string _fileName = string.Empty;

    /// <summary>未指定文件名时，从 URL 推导（缺省扩展名 .mp4，直播合并则可能为 .ts）</summary>
    public string EffectiveFileName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(FileName)) return FileName;
            return ExtractFileNameFromUrl(Url);
        }
    }

    private static string ExtractFileNameFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return $"video_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
        try
        {
            var uri = new Uri(url);
            var lastSegment = uri.AbsolutePath.Split('/').LastOrDefault() ?? "";
            var name = lastSegment;
            var dot = name.LastIndexOf('.');
            if (dot > 0) name = name[..dot];
            if (string.IsNullOrWhiteSpace(name) ||
                name.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".mpd", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".ism", StringComparison.OrdinalIgnoreCase))
            {
                name = $"video_{DateTime.Now:yyyyMMdd_HHmmss}";
            }
            return $"{name}.mp4";
        }
        catch
        {
            return $"video_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
        }
    }
}

/// <summary>解密密钥条目（KID:KEY），用于解密混流页的密钥列表 UI</summary>
public partial class DecryptionKeyEntry : ObservableObject
{
    [ObservableProperty] private string _kid = string.Empty;
    [ObservableProperty] private string _key = string.Empty;
}

/// <summary>混流引入的外部媒体条目（用于解密混流页 UI）</summary>
public partial class MuxImportEntry : ObservableObject
{
    [ObservableProperty] private string _path = string.Empty;
    [ObservableProperty] private string _lang = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
}
