using LYBox.Plugin.Downloader.Models;

namespace LYBox.Plugin.Downloader.Services.Parsers;

/// <summary>
/// MSS (Smooth Streaming) 解析器 —— 占位实现。
/// N_m3u8DL-RE 原生支持 MSS，但本插件当前版本未实现 MSS 解析逻辑，
/// 命中时抛出 NotSupportedException 以便用户明确感知。
/// </summary>
public class MssParser : IPlaylistParser
{
    public bool CanParse(string content)
        => content.Contains("<SmoothStreamingMedia", StringComparison.OrdinalIgnoreCase);

    public Task<ParsedMedia> ParseAsync(string content, string baseUrl, HttpClient client, CancellationToken ct)
        => throw new NotSupportedException("MSS (Smooth Streaming) 解析尚未实现，请使用 HLS 或 DASH 源。");
}
