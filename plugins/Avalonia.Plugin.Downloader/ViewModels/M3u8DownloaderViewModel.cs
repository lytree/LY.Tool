using Avalonia.Plugin.Downloader.Models;
using Avalonia.Plugin.Downloader.Services;
using Avalonia.Plugin.Shared.Attributes;

namespace Avalonia.Plugin.Downloader.ViewModels;

[NavigationItem("Downloader_M3u8")]
[Menu("NAV_Downloader_M3u8", "Downloader_M3u8", ParentKey = "NAV_Downloader", Order = 1)]
[ViewMap(typeof(Pages.M3u8DownloaderPage))]
public partial class M3u8DownloaderViewModel : DownloaderViewModelBase
{
    public override ScriptDescriptor Script => new()
    {
        Id = "m3u8-downloader",
        Name = "M3U8 下载器",
        Description = "下载 M3U8 视频流，支持 AES-128/AES-128-ECB/CHACHA20 加密解密及 FFmpeg 合并",
        Parameters =
        [
            ScriptParameter.Text("url", "M3U8 链接", "M3U8 视频 URL", required: true),
            ScriptParameter.Text("output", "输出文件", "输出文件路径 (如 output.mp4)", required: true),
            ScriptParameter.Number("concurrency", "并发数", "同时下载分片数", 8),
            ScriptParameter.Text("quality", "画质选择", "best/worst/分辨率/带宽", "best"),
            ScriptParameter.Text("headers", "HTTP 请求头", "格式: key=value (多个用逗号分隔)"),
            ScriptParameter.Text("ffmpegPath", "FFmpeg 路径", "ffmpeg 可执行文件路径", "ffmpeg"),
            ScriptParameter.Number("retry", "重试次数", "失败分片重试次数", 3),
        ]
    };

    protected override async Task ExecuteCoreAsync(Dictionary<string, string> paramValues, CancellationToken ct)
    {
        var logger = CreateUiLogger();
        var service = new M3u8DownloadService(logger);

        var headers = ParseHeaders(paramValues.GetValueOrDefault("headers", ""));

        await service.DownloadAsync(
            url: paramValues.GetValueOrDefault("url", ""),
            output: paramValues.GetValueOrDefault("output", "output.mp4"),
            concurrency: int.TryParse(paramValues.GetValueOrDefault("concurrency", "8"), out var c) ? c : 8,
            quality: paramValues.GetValueOrDefault("quality", "best"),
            headers: headers,
            ffmpegPath: paramValues.GetValueOrDefault("ffmpegPath", "ffmpeg"),
            retryCount: int.TryParse(paramValues.GetValueOrDefault("retry", "3"), out var r) ? r : 3,
            ct: ct);
    }

    private static Dictionary<string, string>? ParseHeaders(string? headerStr)
    {
        if (string.IsNullOrWhiteSpace(headerStr)) return null;

        var headers = new Dictionary<string, string>();
        foreach (var pair in headerStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = pair.IndexOf('=');
            if (idx > 0)
            {
                var key = pair[..idx];
                var value = pair[(idx + 1)..];
                headers[key] = value;
            }
        }
        return headers.Count > 0 ? headers : null;
    }
}
