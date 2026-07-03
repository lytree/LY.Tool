using System.Net;
using LYBox.Plugin.Downloader.Models;

namespace LYBox.Plugin.Downloader.Services;

/// <summary>
/// 根据 DownloadOptions 构建 HttpClient（含 headers / proxy / timeout / User-Agent）。
/// </summary>
public static class HttpClientFactory
{
    private const string DefaultUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    public static HttpClient Create(DownloadOptions opts)
    {
        var handler = new HttpClientHandler
        {
            MaxConnectionsPerServer = Math.Max(16, opts.ThreadCount),
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true
        };

        // 代理：优先显式 custom-proxy，其次系统代理
        if (!string.IsNullOrWhiteSpace(opts.Proxy))
        {
            try { handler.Proxy = new WebProxy(opts.Proxy); handler.UseProxy = true; }
            catch { /* 非法代理字符串忽略 */ }
        }
        else
        {
            handler.UseProxy = opts.UseSystemProxy;
        }

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(10, opts.HttpRequestTimeout))
        };
        client.DefaultRequestHeaders.Add("User-Agent", DefaultUserAgent);

        foreach (var kv in opts.Headers)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(kv.Key, kv.Value);
        }
        return client;
    }
}

/// <summary>URL 解析工具（相对 URL → 绝对 URL）</summary>
public static class UrlUtils
{
    public static string Resolve(string baseUrl, string relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl)) return relativeUrl;
        if (relativeUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            relativeUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            relativeUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            relativeUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return relativeUrl;
        }
        try
        {
            var baseUri = new Uri(baseUrl);
            if (relativeUrl.StartsWith('/'))
                return $"{baseUri.Scheme}://{baseUri.Authority}{relativeUrl}";
            return new Uri(baseUri, relativeUrl).ToString();
        }
        catch
        {
            return relativeUrl;
        }
    }
}
