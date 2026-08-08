using System.Net;

namespace LYBox.Plugin.Shared.Web;

/// <summary>
/// Web 插件页面展示层的开发辅助：路由显示、导航信任校验与开发错误页。
/// 从公司版 PluginWebViewPage 的展示层特性精简移植，仅保留与宿主 WebView 展示相关的部分。
/// </summary>
internal static class PluginWebViewDevTools
{
    /// <summary>JS 重试消息（错误页 Retry 按钮经 invokeCSharpAction 发出，与 WebViewIpcTransport 通道一致）。</summary>
    internal const string RetryMessageKind = "lybox-plugin-retry";

    /// <summary>
    /// 校验目标 URI 是否位于授权基地址（同 scheme/host/port 且路径位于基路径之下）。
    /// 用于 NavigationStarted 拦截越权导航，避免 Web 插件页面跳转到外部地址。
    /// </summary>
    internal static bool IsAllowedNavigation(Uri candidate, Uri authorizedBaseUri)
    {
        if (!candidate.IsAbsoluteUri || !authorizedBaseUri.IsAbsoluteUri)
            return false;

        if (!string.IsNullOrEmpty(candidate.UserInfo) || !string.IsNullOrEmpty(authorizedBaseUri.UserInfo))
            return false;

        if (!string.Equals(candidate.Scheme, authorizedBaseUri.Scheme, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(candidate.IdnHost, authorizedBaseUri.IdnHost, StringComparison.OrdinalIgnoreCase))
            return false;

        if (candidate.Port != authorizedBaseUri.Port)
            return false;

        // 候选路径必须位于授权基路径之下（或相等）
        var candidatePath = NormalizePath(candidate.AbsolutePath);
        var basePath = NormalizePath(authorizedBaseUri.AbsolutePath);
        return candidatePath.Equals(basePath, StringComparison.OrdinalIgnoreCase)
            || candidatePath.StartsWith(basePath.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)
            || basePath is "/" or "";
    }

    /// <summary>
    /// 从页面路由与入口 URI 计算路由基路径，用于在 Route 栏中显示相对路径。
    /// </summary>
    internal static string GetRouteBasePath(Uri pageUri, string route)
    {
        if (!pageUri.IsAbsoluteUri)
            return string.Empty;

        var normalizedRoute = string.IsNullOrWhiteSpace(route) || route == "/"
            ? "/"
            : "/" + route.TrimStart('/');
        var path = pageUri.AbsolutePath;
        if (normalizedRoute == "/")
            return path.TrimEnd('/');

        return path.EndsWith(normalizedRoute, StringComparison.OrdinalIgnoreCase)
            ? path[..^normalizedRoute.Length].TrimEnd('/')
            : string.Empty;
    }

    /// <summary>提取用于 Route 栏显示的相对路径 + 查询串。</summary>
    internal static string GetRouteText(Uri uri, string routeBasePath)
    {
        if (!uri.IsAbsoluteUri)
            return uri.ToString();

        var path = uri.AbsolutePath;
        if (!string.IsNullOrEmpty(routeBasePath)
            && path.StartsWith(routeBasePath, StringComparison.OrdinalIgnoreCase))
        {
            path = path[routeBasePath.Length..];
        }

        if (string.IsNullOrEmpty(path))
            path = "/";
        else if (path[0] != '/')
            path = "/" + path;

        return path + uri.Query;
    }

    /// <summary>生成开发错误页 HTML（带 Retry 按钮，经 invokeCSharpAction 发送重试消息）。</summary>
    internal static string CreateDevelopmentErrorHtml(string pluginId, string targetUri, string reason)
    {
        static string Encode(string value) => WebUtility.HtmlEncode(value);

        return $$"""
            <!doctype html>
            <html lang="zh-CN">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Web 插件页面不可用</title>
            </head>
            <body>
              <main>
                <h1>Web 插件页面不可用</h1>
                <dl>
                  <dt>Plugin</dt><dd>{{Encode(pluginId)}}</dd>
                  <dt>Target</dt><dd>{{Encode(targetUri)}}</dd>
                  <dt>Reason</dt><dd>{{Encode(reason)}}</dd>
                </dl>
                <button type="button" onclick="const m=JSON.stringify({kind:'lybox-plugin-retry'});if(typeof window.invokeCSharpAction==='function')window.invokeCSharpAction(m);else if(window.chrome&amp;&amp;window.chrome.webview)window.chrome.webview.postMessage(m)">Retry</button>
              </main>
            </body>
            </html>
            """;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "/";

        // 合并重复斜杠，去掉末尾斜杠（根路径除外）
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var normalized = "/" + string.Join('/', parts);
        return normalized.Length == 1 ? "/" : normalized;
    }
}
