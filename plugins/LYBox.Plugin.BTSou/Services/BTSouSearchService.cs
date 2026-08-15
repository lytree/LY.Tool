using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LYBox.Plugin.BTSou.Models;

namespace LYBox.Plugin.BTSou.Services;

/// <summary>
/// BTSOU 资源池解析与搜索服务。
/// 资源池文件为文本格式，以 \r\n 分行、\t 分列：
///   配置行：版本=\t... 屏蔽词=\t... 广告词=\t... 库=\t...（20 列资源模板）
///   资源行：每行 20 个 \t 分隔字段（搜索引擎模板 URL、编码、分隔符等）
/// 搜索流程：关键词 → 匹配来源库 → 构造搜索 URL → 抓取 → 按分隔符解析出磁链/ed2k → 展示。
/// </summary>
public class BTSouSearchService
{
    /// <summary>静态单例（ViewModel 由生成器无参构造，服务经单例访问）</summary>
    public static BTSouSearchService Current { get; } = new();

    private readonly HttpClient _http;
    private readonly HashSet<string> _blockedWords = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>资源池原始文本</summary>
    public string? ResPoolRaw { get; private set; }

    /// <summary>屏蔽词列表（来自资源池"屏蔽词="配置）</summary>
    public IReadOnlyCollection<string> BlockedWords => _blockedWords;

    /// <summary>版本号（来自资源池"版本="配置）</summary>
    public string? Version { get; private set; }

    /// <summary>搜索关键词黑名单（原程序在关键词里命中屏蔽词则禁止搜索）</summary>
    public string[] BlockedKeywordPrefixes { get; set; } = [];

    public BTSouSearchService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (BTSou/24.10)");
    }

    /// <summary>
    /// 加载资源池：优先本地缓存文件，否则从服务器下载。
    /// </summary>
    public async Task<string> LoadResourcePoolAsync(string? localCachePath = null, CancellationToken ct = default)
    {
        if (localCachePath != null && System.IO.File.Exists(localCachePath))
        {
            ResPoolRaw = await System.IO.File.ReadAllTextAsync(localCachePath, Encoding.UTF8, ct);
        }
        else
        {
            var bytes = await _http.GetByteArrayAsync(BTSouConfig.ResPoolUrl, ct);
            ResPoolRaw = Encoding.UTF8.GetString(bytes);
            if (localCachePath != null)
                await System.IO.File.WriteAllTextAsync(localCachePath, ResPoolRaw, Encoding.UTF8, ct);
        }
        ParseConfig(ResPoolRaw);
        return ResPoolRaw;
    }

    /// <summary>
    /// 解析资源池配置（版本/屏蔽词/广告词等键值行 + "库=" 后的资源模板）。
    /// </summary>
    private void ParseConfig(string raw)
    {
        Version = GetConfigValue(raw, "版本=\t");
        var blocked = GetConfigValue(raw, "屏蔽词=\t");
        _blockedWords.Clear();
        foreach (var w in (blocked ?? "").Split([',', '\t', '，'], StringSplitOptions.RemoveEmptyEntries))
            _blockedWords.Add(w.Trim());
    }

    private static string? GetConfigValue(string raw, string key)
    {
        var idx = raw.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0) return null;
        var start = idx + key.Length;
        var end = raw.IndexOf('\r', start);
        return end < 0 ? raw[start..] : raw[start..end];
    }

    /// <summary>
    /// 检查关键词是否命中屏蔽词（原程序：关键词 Contains 屏蔽词则拒绝搜索）。
    /// </summary>
    public bool ContainsBlockedWord(string keyword)
    {
        var upper = keyword.ToUpperInvariant();
        foreach (var w in _blockedWords)
        {
            if (!string.IsNullOrEmpty(w) && upper.Contains(w.ToUpperInvariant()))
                return true;
        }
        foreach (var p in BlockedKeywordPrefixes)
        {
            if (!string.IsNullOrEmpty(p) && upper.Contains(p.ToUpperInvariant()))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 过滤掉搜索结果中的广告词（原程序按来源 URL 域名过滤广告站）。
    /// </summary>
    public bool IsAdSource(string sourceUrl)
    {
        if (string.IsNullOrEmpty(sourceUrl)) return false;
        var upper = sourceUrl.ToUpperInvariant();
        return upper.Contains(".CN") || upper.Contains(".CL") || upper.Contains(".WS")
            || upper.Contains(".NE") || upper.Contains(".TO") || upper.Contains(".BU")
            || upper.Contains(".CC") || upper.Contains(".CO");
    }

    /// <summary>
    /// 关键词分词（去掉标点符号，原程序用固定字符集 Split）。
    /// </summary>
    public static string[] TokenizeKeyword(string keyword)
    {
        char[] separators = ['《', '》', '、', '【', '】', '。', '，', '：', '；', '？',
            '！', '{', '}', '[', ']', ',', '.', ':', '?', '!',
            '<', '>', '_', '-', '`', '—', '·', '（', '）', '+',
            '(', ')', '\'', '"', '“', '”', ' ', '\u3000'];
        return keyword.Split(separators, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// 生成磁力链接（原程序：magnet:?xt=urn:btih: + hash）。
    /// </summary>
    public static string BuildMagnet(string hash) => "magnet:?xt=urn:btih:" + hash;

    /// <summary>
    /// 从 ed2k 文本中截取完整链接。
    /// </summary>
    public static string ExtractEd2k(string text, string marker)
    {
        var idx = text.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return text;
        var start = idx;
        var end = text.IndexOf('"', start);
        return end < 0 ? text[start..] : text[start..end];
    }

    /// <summary>
    /// 繁体转简体（复刻原程序：Win32 LCMapString，LOCALE_SYSTEM_DEFAULT + LCMAP_SIMPLIFIED_CHINESE）。
    /// </summary>
    public static string ToSimplifiedChinese(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        try
        {
            var buf = new string(' ', text.Length);
            NativeMethods.LCMapString(2048, 33554432, text, text.Length, buf, text.Length);
            return buf;
        }
        catch
        {
            return text;
        }
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("kernel32", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        public static extern int LCMapString(int locale, int flags, string src, int cchSrc,
            string dst, int cchDst);
    }

    /// <summary>
    /// 从资源池"库="配置中解析来源库列表（每行 20 列：URL 模板等）。
    /// </summary>
    public List<string[]> ParseSourceLibraries(string raw)
    {
        var list = new List<string[]>();
        var idx = raw.LastIndexOf("库=", StringComparison.Ordinal);
        if (idx < 0) return list;
        var text = raw[(idx + 3)..];
        var lines = text.Split(["\r\n"], StringSplitOptions.None);
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Split('\t');
            if (cols.Length >= 6)
                list.Add(cols);
        }
        return list;
    }

    /// <summary>
    /// 将资源池按行拆分为可搜索的原始条目（原程序：按 \r\n 分行、Contains 来源名+\t 过滤）。
    /// </summary>
    public IEnumerable<string> EnumeratePoolLines(string raw)
    {
        return raw.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries)
                  .Where(l => !l.StartsWith("版本=") && !l.StartsWith("屏蔽词=") && !l.StartsWith("广告词="));
    }
}
