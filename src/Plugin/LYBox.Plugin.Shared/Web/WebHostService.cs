using System.Collections.Concurrent;
using System.Text.Json;
using LYBox.Plugin.Shared.Rpc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace LYBox.Plugin.Shared.Web;

/// <summary>
/// 嵌入式 Kestrel HTTP 资源服务。单进程内全局唯一，启动在 <c>127.0.0.1</c> 自动分配端口。
/// 提供：
/// <list type="bullet">
/// <item><c>GET /{pluginId}/{**path}</c>：服务插件的 <c>wwwroot/</c> 静态资源</item>
/// <item><c>GET /sse/{pluginId}</c>：SSE 长连接，C# 主动推送事件 / 通道数据</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// 生命周期：在 <c>App.Initialize()</c> 中通过 DI 注册为单例，<c>ServiceProvider</c> 构建后调用
/// <see cref="StartAsync"/>；应用退出时随 <c>ServiceProvider.Dispose()</c> 自动停止
/// （实现 <see cref="IAsyncDisposable"/>）。
/// </para>
/// <para>
/// 路由注册：每个 Web 插件通过 <see cref="MapPluginRoot"/> 注册其 <c>wwwroot</c> 目录，
/// 请求 <c>/{pluginId}/foo.css</c> 时按 pluginId 查找目录后通过 <c>StaticFiles</c> 中间件返回。
/// </para>
/// <para>
/// 端口分配：使用 <c>http://127.0.0.1:0</c> 让系统自动分配端口，启动后通过 <see cref="Port"/>
/// 暴露实际端口；<c>WebPluginView</c> 据此构造 <c>WebView.Source</c>。
/// </para>
/// </remarks>
public sealed class WebHostService : IAsyncDisposable
{
    private WebApplication? _app;
    private readonly ConcurrentDictionary<string, string> _pluginRoots = new();
    private readonly ConcurrentDictionary<string, RpcCommandHandler> _rpcHandlers = new();
    private readonly SseEventPusher _ssePusher = new();

    /// <summary>当前监听端口。0 表示尚未启动。</summary>
    public int Port { get; private set; }

    /// <summary>资源服务 BaseUrl，例如 <c>http://127.0.0.1:54321</c>。</summary>
    public string BaseUrl => $"http://127.0.0.1:{Port}";

    /// <summary>SSE 推送器（供 <c>WebViewIpcHost</c> 注入）。</summary>
    public IEventPusher EventPusher => _ssePusher;

    /// <summary>
    /// 注册一个 RPC 命令处理器。供浏览器模式 HTTP 桥接使用（POST /__rpc）。
    /// 由 <see cref="WebViewIpcHost.RegisterCommand"/> 同步调用，确保 WebView 与浏览器两种传输层共享同一套命令。
    /// </summary>
    /// <param name="name">命令短名（如 <c>GreetAsync</c>），需与前端 <c>window.__lybox.rpc(name, ...)</c> 的 name 参数一致。</param>
    /// <param name="handler">命令处理器。</param>
    public void RegisterRpcHandler(string name, RpcCommandHandler handler)
        => _rpcHandlers[name] = handler;

    /// <summary>获取所有已注册 RPC 命令名（供调试面板展示）。</summary>
    public IReadOnlyCollection<string> GetRegisteredRpcCommands() => (IReadOnlyCollection<string>)_rpcHandlers.Keys;

    /// <summary>
    /// 注册一个 Web 插件的静态资源根目录。
    /// 必须在 <see cref="StartAsync"/> 之前或之后均可调用（线程安全的字典写入），
    /// 但建议在启动前注册所有插件以保证首请求即可服务。
    /// </summary>
    /// <param name="pluginId">插件 ID（路由前缀）。</param>
    /// <param name="wwwrootPath">插件 <c>wwwroot/</c> 绝对路径。</param>
    public void MapPluginRoot(string pluginId, string wwwrootPath)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            throw new ArgumentException("pluginId 不能为空", nameof(pluginId));
        if (string.IsNullOrWhiteSpace(wwwrootPath))
            throw new ArgumentException("wwwrootPath 不能为空", nameof(wwwrootPath));

        // 开发模式回退：bin 目录下无 wwwroot 时，尝试源码目录
        var resolvedPath = wwwrootPath;
        if (!Directory.Exists(resolvedPath))
        {
            var devPath = ResolveDevWwwroot(pluginId);
            if (devPath is not null && Directory.Exists(devPath))
            {
                resolvedPath = devPath;
                Console.Error.WriteLine(
                    "[WebHostService] 插件 {0} 使用开发模式 wwwroot: {1}", pluginId, devPath);
            }
        }

        if (!Directory.Exists(resolvedPath))
            throw new DirectoryNotFoundException($"wwwroot 目录不存在: {wwwrootPath}");

        _pluginRoots[pluginId] = resolvedPath;
    }

    /// <summary>
    /// 开发模式下定位插件源码目录的 wwwroot。
    /// 优先：环境变量 LYBOX_PLUGIN_SRC_{PluginId}（下划线替换连字符）。
    /// 回退：从 AVALONIA_EXTRA_PLUGINS_PATH 向上遍历查找含 wwwroot 子目录的祖先。
    /// </summary>
    private static string? ResolveDevWwwroot(string pluginId)
    {
        var envKey = $"LYBOX_PLUGIN_SRC_{pluginId.Replace("-", "_")}";
        var envPath = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrEmpty(envPath))
        {
            var candidate = Path.Combine(envPath, "wwwroot");
            if (Directory.Exists(candidate)) return candidate;
        }

        var extraPath = Environment.GetEnvironmentVariable("AVALONIA_EXTRA_PLUGINS_PATH");
        if (!string.IsNullOrEmpty(extraPath))
        {
            var dir = new DirectoryInfo(extraPath);
            for (var i = 0; i < 6 && dir is not null; i++)
            {
                var candidate = Path.Combine(dir.FullName, "wwwroot");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }

        return null;
    }

    /// <summary>启动 Kestrel 监听。</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null) return; // 幂等

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        _app = builder.Build();

        // —— SSE 端点 ——
        _app.MapGet("/sse/{pluginId}", async (string pluginId, HttpContext ctx) =>
        {
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers["Cache-Control"] = "no-cache";
            ctx.Response.Headers["Connection"] = "keep-alive";
            ctx.Response.Headers["X-Accel-Buffering"] = "no"; // 禁用 Nginx 缓冲（兼容反向代理场景）

            // 先发 ready 事件让前端确认连接已建立
            var readyPayload = $"{{\"pluginId\":\"{pluginId}\"}}";
            await ctx.Response.WriteAsync($"event: ready\ndata: {readyPayload}\n\n", ctx.RequestAborted).ConfigureAwait(false);
            await ctx.Response.Body.FlushAsync(ctx.RequestAborted).ConfigureAwait(false);

            var client = new SseClient(ctx.Response.Body);
            _ssePusher.Subscribe(pluginId, client);
            try
            {
                // 阻塞直到客户端断开
                await Task.Delay(Timeout.Infinite, ctx.RequestAborted).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 客户端断开连接，正常退出
            }
            finally
            {
                _ssePusher.Unsubscribe(pluginId, client);
            }
        });

        // —— HTTP RPC 桥接端点（浏览器模式 / 调试面板使用）——
        // POST /__rpc，body = {name, args, callbackId?}
        // 响应 {result: ...} 或 {error: "..."}
        // 复用 WebViewIpcHost.RegisterCommand 同步注册到本服务的同一套 handler，确保 WebView 与浏览器行为一致。
        _app.MapPost("/__rpc", async (HttpContext ctx) =>
        {
            HttpRequestRpcBody? body;
            try
            {
                body = await JsonSerializer.DeserializeAsync<HttpRequestRpcBody>(ctx.Request.Body, RpcEnvelope.JsonOptions, ctx.RequestAborted).ConfigureAwait(false);
            }
            catch
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.Response.WriteAsJsonAsync(new { error = "请求体无效：需 JSON 格式 {name, args, callbackId?}" }, ctx.RequestAborted).ConfigureAwait(false);
                return;
            }
            if (body is null || string.IsNullOrEmpty(body.Name))
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.Response.WriteAsJsonAsync(new { error = "name 字段不能为空" }, ctx.RequestAborted).ConfigureAwait(false);
                return;
            }

            if (!_rpcHandlers.TryGetValue(body.Name, out var handler))
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                await ctx.Response.WriteAsJsonAsync(new { error = $"命令未注册: {body.Name}" }, ctx.RequestAborted).ConfigureAwait(false);
                return;
            }

            try
            {
                var args = body.Args ?? Array.Empty<JsonElement>();
                var result = await handler(args, ctx.RequestAborted).ConfigureAwait(false);
                await ctx.Response.WriteAsJsonAsync(new { result }, RpcEnvelope.JsonOptions, cancellationToken: ctx.RequestAborted).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await ctx.Response.WriteAsJsonAsync(new { error = ex.Message }, RpcEnvelope.JsonOptions, cancellationToken: ctx.RequestAborted).ConfigureAwait(false);
            }
        });

        // —— 事件 emit 端点（浏览器模式，前端 __lybox.emit 走 HTTP）——
        // POST /__emit，body = {name, data}
        // 当前为接收端存根：浏览器模式无 WebViewIpcHost 消费事件，仅记录日志。
        _app.MapPost("/__emit", (HttpContext ctx) =>
        {
            // 浏览器模式的事件 emit 一般无业务需求，返回 202 Accepted 即可。
            ctx.Response.StatusCode = StatusCodes.Status202Accepted;
            return Task.CompletedTask;
        });

        // —— 通道关闭端点（浏览器模式）——
        _app.MapPost("/__channel/close", (HttpContext ctx) =>
        {
            ctx.Response.StatusCode = StatusCodes.Status202Accepted;
            return Task.CompletedTask;
        });

        // —— 调试面板端点（仅 Debug 配置启用）——
        // GET /__lybox/debug 返回纯 HTML 调试器，列出所有已注册 RPC 命令 + SSE 事件流查看器。
#if DEBUG
        _app.MapGet("/__lybox/debug", () => Results.Content(
            DebugPanelHtml.Render(_rpcHandlers.Keys),
            "text/html; charset=utf-8"));
#endif

        // —— 静态资源端点：按 pluginId 路由分发 ——
        // 路由模板：/{pluginId}/{**path}，catch-all 参数 path 可能包含子目录分隔符
        _app.MapGet("/{pluginId}/{**path}", async (string pluginId, string path, HttpContext ctx) =>
        {
            if (!_pluginRoots.TryGetValue(pluginId, out var root))
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                await ctx.Response.WriteAsync($"Plugin '{pluginId}' not registered.", ctx.RequestAborted).ConfigureAwait(false);
                return;
            }

            // 防目录穿越：标准化路径后确保结果仍在 root 之下
            var rootFull = Path.GetFullPath(root);
            var combined = Path.GetFullPath(Path.Combine(rootFull, path.Replace('/', Path.DirectorySeparatorChar)));
            if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            if (!File.Exists(combined))
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            ctx.Response.ContentType = GuessMimeType(combined);
            await ctx.Response.SendFileAsync(combined, ctx.RequestAborted).ConfigureAwait(false);
        });

        // —— 根路径健康检查 ——
        _app.MapGet("/", () => Results.Ok(new { service = "LYBox.WebHostService", version = "1.0" }));

        await _app.StartAsync(cancellationToken).ConfigureAwait(false);

        // 从实际监听地址提取端口
        Port = ExtractPort(_app.Urls.FirstOrDefault());
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            try
            {
                await _app.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch { /* 关闭过程中止：忽略，继续 DisposeAsync */ }
            await _app.DisposeAsync().ConfigureAwait(false);
            _app = null;
        }
        Port = 0;
    }

    private static int ExtractPort(string? url)
    {
        if (string.IsNullOrEmpty(url)) return 0;
        // URL 形如 http://127.0.0.1:54321
        var colonIndex = url.LastIndexOf(':');
        if (colonIndex < 0 || colonIndex == url.Length - 1) return 0;
        return int.TryParse(url.AsSpan(colonIndex + 1), out var port) ? port : 0;
    }

    private static string GuessMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".js" or ".mjs" => "application/javascript; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".ico" => "image/x-icon",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".eot" => "application/vnd.ms-fontobject",
            ".map" => "application/json; charset=utf-8",
            ".webp" => "image/webp",
            ".webmanifest" => "application/manifest+json",
            ".wasm" => "application/wasm",
            _ => "application/octet-stream",
        };
    }
}

/// <summary>POST /__rpc 请求体。callbackId 在浏览器模式下由 ipc.js 内部使用，HTTP 响应直接返回 result。</summary>
internal sealed class HttpRequestRpcBody
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("args")]
    public JsonElement[]? Args { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("callbackId")]
    public string? CallbackId { get; set; }
}
