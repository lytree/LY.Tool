using LYBox.Plugin.Downloader.Models;

namespace LYBox.Plugin.Downloader.Services.Parsers;

/// <summary>播放清单解析器接口</summary>
public interface IPlaylistParser
{
    /// <summary>是否可处理该输入（基于内容嗅探）</summary>
    bool CanParse(string content);

    /// <summary>解析清单为媒体结构</summary>
    Task<ParsedMedia> ParseAsync(string content, string baseUrl, HttpClient client, CancellationToken ct);
}
