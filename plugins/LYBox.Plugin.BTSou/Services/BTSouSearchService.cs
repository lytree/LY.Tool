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
/// BTSOU 资源搜索服务（精简版，仅保留搜索 + 迅雷下载）。
/// 还原原程序核心逻辑：资源池解析、来源库模板、屏蔽词/广告过滤、
/// 磁力/ed2k/thunder 链接解析、相对时间换算、迅雷一键下载。
/// </summary>
public class BTSouSearchService : IDisposable
{
    private readonly HttpClient _http;

    /// <summary>资源池原始文本</summary>
    public string? ResPoolRaw { get; private set; }

    /// <summary>屏蔽词（资源池"屏蔽词="配置，命中则拒绝搜索）</summary>
    public string[] BlockedWords { get; private set; } = [];

    /// <summary>广告词（资源池"广告词="配置，命中则从标题剔除）</summary>
    public string[] AdWords { get; private set; } = [];

    /// <summary>智能过滤词（"智能滤="配置，标题必须全部包含才展示）</summary>
    public string[] SmartFilterWords { get; private set; } = [];

    /// <summary>热搜关键词（"热搜="配置，// 分隔）</summary>
    public string[] HotWords { get; private set; } = [];

    /// <summary>来源库列表（"库="段之后每行 20 列模板）</summary>
    public List<string[]> SourceLibraries { get; private set; } = [];

    /// <summary>资源池版本</summary>
    public string? Version { get; private set; }

    public BTSouSearchService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (BTSou/24.10)");
    }

    /// <summary>释放 HttpClient（由 DI 容器在应用退出时调用）</summary>
    public void Dispose() => _http.Dispose();

    // ==================== 资源池加载与解析 ====================

    /// <summary>
    /// 加载资源池：优先本地缓存，否则从服务器下载并缓存。
    /// </summary>
    public async Task LoadResourcePoolAsync(string? localCachePath = null, CancellationToken ct = default)
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
    }

    private void ParseConfig(string raw)
    {
        Version = GetConfigValue(raw, "版本=\t");
        BlockedWords = SplitCsv(GetConfigValue(raw, "屏蔽词=\t"));
        AdWords = SplitCsv(GetConfigValue(raw, "广告词=\t"));
        SmartFilterWords = SplitCsv(GetConfigValue(raw, "智能滤=\t"));
        var hot = GetConfigValue(raw, "热搜=\t");
        HotWords = (hot ?? "").Replace("//", "\t").Replace("|", "\r")
            .Split(['\t', '\r'], StringSplitOptions.RemoveEmptyEntries);

        SourceLibraries.Clear();
        var idx = raw.LastIndexOf("库=", StringComparison.Ordinal);
        if (idx < 0) return;
        var text = raw[(idx + 3)..];
        var lines = text.Split(["\r\n"], StringSplitOptions.None);
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Split('\t');
            if (cols.Length >= 6)
                SourceLibraries.Add(cols);
        }
    }

    private static string? GetConfigValue(string raw, string key)
    {
        var idx = raw.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0) return null;
        var start = idx + key.Length;
        var end = raw.IndexOf('\r', start);
        return end < 0 ? raw[start..] : raw[start..end];
    }

    private static string[] SplitCsv(string? s) =>
        (s ?? "").Split([',', '\t', '，'], StringSplitOptions.RemoveEmptyEntries)
                 .Select(x => x.Trim()).ToArray();

    // ==================== 搜索（还原原程序 ah_1 / a_3 核心） ====================

    /// <summary>检查关键词是否命中屏蔽词（原逻辑：Contains 即拒绝）</summary>
    public bool ContainsBlockedWord(string keyword)
    {
        var upper = keyword.ToUpperInvariant();
        foreach (var w in BlockedWords)
        {
            if (!string.IsNullOrEmpty(w) && upper.Contains(w.ToUpperInvariant()))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 还原原程序 a_3 的搜索：给定来源库模板和关键词，模拟抓取解析出结果。
    /// 原程序为真实 HTTP 抓取第三方站点；此处以资源池行内匹配 + 模板字段还原展示格式，
    /// 若需真实抓取可替换 _FetchPage 实现。
    /// </summary>
    public async Task SearchAsync(string keyword, string? sourceLibrary, int maxPerSource,
        Func<SearchResultItem, CancellationToken, Task> onResult, CancellationToken ct = default)
    {
        var kw = ToSimplifiedChinese(keyword);
        var raw = ResPoolRaw ?? "";
        var lines = raw.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries)
                       .Where(l => !l.StartsWith("版本=") && !l.StartsWith("屏蔽词=")
                                && !l.StartsWith("广告词=") && !l.StartsWith("智能滤=")
                                && !l.StartsWith("热搜=") && !l.StartsWith("女优="))
                       .ToList();

        var targets = sourceLibrary is null or "Search All"
            ? SourceLibraries.Select(l => l[0]).ToList()
            : SourceLibraries.Where(l => l[0].Contains(sourceLibrary)).Select(l => l[0]).ToList();

        var matched = 0;
        foreach (var line in lines)
        {
            ct.ThrowIfCancellationRequested();
            var cols = line.Split('\t');
            if (cols.Length < 5) continue;
            var haystack = ToSimplifiedChinese(cols[0] + "\t" + line).ToLowerInvariant();
            if (!haystack.Contains(kw.ToLowerInvariant())) continue;

            var item = new SearchResultItem
            {
                Title = CleanTitle(cols[0]),
                Size = ParseSize(cols.Length > 1 ? cols[1] : ""),
                UpdateTime = ParseRelativeTime(cols.Length > 2 ? cols[2] : ""),
                Source = targets.Contains(cols[3] ?? "") ? cols[3] : "",
                Link = NormalizeLink(cols.Length > 4 ? cols[4] : "")
            };
            if (item.Link.Length == 0) continue;

            matched++;
            if (matched >= maxPerSource * Math.Max(1, targets.Count)) break;
            await onResult(item, ct);
        }
    }

    /// <summary>清理标题：剔除广告词（还原原程序 e.b.a[0] 广告过滤）</summary>
    public string CleanTitle(string title)
    {
        var t = ToSimplifiedChinese(title);
        foreach (var ad in AdWords)
        {
            if (!string.IsNullOrEmpty(ad))
                t = t.Replace(ad, "");
        }
        return t.Trim();
    }

    /// <summary>解析文件大小：正则 \b\d+(\.\d+)?\s?(GB|MB|KB)\b（还原 e.b.a[2]）</summary>
    public static string ParseSize(string text)
    {
        var m = Regex.Match(text ?? "", @"\b\d+(\.\d+)?\s?(GB|MB|KB)\b", RegexOptions.IgnoreCase);
        return m.Success ? m.Value.Trim().ToUpperInvariant() : "";
    }

    /// <summary>
    /// 解析相对时间（还原 e.b.a[1]）：
    /// X天/日/D 前 → 减X天；X星期 前 → 减X*7天；X月/M 前 → 减X*30天；X年/Y 前 → 减X*365天；
    /// 否则尝试匹配 yyyy-MM-dd 直接格式。
    /// </summary>
    public static string ParseRelativeTime(string text)
    {
        try
        {
            var t = text ?? "";
            if (t.Length == 0) return "????-??-??";
            if (t.Contains("星期"))
            {
                var n = int.Parse(Regex.Match(t, @"\d+").Value);
                return DateTime.Now.AddDays(-n * 7).ToString("yyyy-MM-dd");
            }
            if (t.Contains("月") || t.Contains("M"))
            {
                var n = int.Parse(Regex.Match(t, @"\d+").Value);
                return DateTime.Now.AddDays(-n * 30).ToString("yyyy-MM-dd");
            }
            if (t.Contains("年") || t.Contains("Y"))
            {
                var n = int.Parse(Regex.Match(t, @"\d+").Value);
                return DateTime.Now.AddDays(-n * 365).ToString("yyyy-MM-dd");
            }
            if (t.Contains("小时") || t.Contains("H"))
            {
                var n = int.Parse(Regex.Match(t, @"\d+").Value);
                return DateTime.Now.AddDays(-(double)n / 24.0).ToString("yyyy-MM-dd");
            }
            if (t.Contains("日") || t.Contains("天") || t.Contains("D"))
            {
                var n = int.Parse(Regex.Match(t, @"\d+").Value);
                return DateTime.Now.AddDays(-n).ToString("yyyy-MM-dd");
            }
            var m = Regex.Match(t, @"([0-9]{4}-[0-9]{2}-[0-9]{2})");
            if (m.Success) return m.Value;
            return "????-??-??";
        }
        catch
        {
            return "????-??-??";
        }
    }

    /// <summary>规范化下载链接：magnet / ed2k / thunder / http（还原 e.b.a[4] 组装）</summary>
    public static string NormalizeLink(string raw)
    {
        var link = raw?.Trim() ?? "";
        if (link.Length == 0) return "";

        // 纯 40 位哈希 → 磁力
        if (link.Length >= 16 && link.Length <= 56 && Regex.IsMatch(link, "^[0-9a-zA-Z]+$"))
            return "magnet:?xt=urn:btih:" + link.Split('&')[0];

        // 已带协议前缀
        if (link.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase)
            || link.StartsWith("ed2k:", StringComparison.OrdinalIgnoreCase)
            || link.StartsWith("thunder:", StringComparison.OrdinalIgnoreCase)
            || link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return link;

        return "";
    }

    /// <summary>提取磁力哈希</summary>
    public static string? ExtractBtih(string magnetLink)
    {
        if (!magnetLink.StartsWith("magnet:?xt=urn:btih:", StringComparison.OrdinalIgnoreCase))
            return null;
        var rest = magnetLink["magnet:?xt=urn:btih:".Length..];
        return rest.Split('&')[0];
    }

    // ==================== 迅雷下载（还原 c_3） ====================

    /// <summary>
    /// 调用迅雷下载（还原原程序 c_3：AgentClass.AddTask + CommitTasks2）。
    /// 依赖本机安装迅雷；失败返回 false。
    /// </summary>
    public static bool DownloadWithThunder(string link)
    {
        try
        {
            var agent = new ThunderAgentLib.AgentClass();
            agent.AddTask(link, "", "", "", "", 0, 0, 5);
            agent.CommitTasks2(1);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ==================== 编码工具（还原 b_7 / a_14） ====================

    /// <summary>繁体转简体（复刻 Win32 LCMapString，LOCALE_SYSTEM_DEFAULT + LCMAP_SIMPLIFIED_CHINESE）</summary>
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
}
