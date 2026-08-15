using System;

namespace LYBox.Plugin.BTSou.Models;

/// <summary>
/// 搜索结果条目（对应原程序资源池中一行记录解析后的展示行）。
/// </summary>
public class SearchResultItem
{
    /// <summary>资源标题</summary>
    public string Title { get; set; } = "";

    /// <summary>文件大小</summary>
    public string Size { get; set; } = "";

    /// <summary>更新时间</summary>
    public string UpdateTime { get; set; } = "";

    /// <summary>来源（搜索引擎名）</summary>
    public string Source { get; set; } = "";

    /// <summary>下载链接（magnet:/ed2k:/http）</summary>
    public string Link { get; set; } = "";

    /// <summary>磁力哈希（从 magnet 链接提取）</summary>
    public string? MagnetHash
    {
        get
        {
            if (Link.StartsWith("magnet:?xt=urn:btih:"))
            {
                var parts = Link["magnet:?xt=urn:btih:".Length..].Split('&');
                return parts.Length > 0 ? parts[0] : null;
            }
            return null;
        }
    }

    /// <summary>种子文件镜像 URL（bt.box.n0808.com）</summary>
    public string? TorrentMirrorUrl =>
        MagnetHash is { } h ? BTSouConfig.TorrentMirrorBase + h + ".torrent" : null;

    /// <summary>是否为磁力链接</summary>
    public bool IsMagnet => Link.StartsWith("magnet:?xt=urn:btih:");
}
