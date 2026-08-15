// LYBox Mock Server —— 前端调试 Mock 后端
//
// 发布为 dotnet tool，无需启动 Avalonia 即可调试前端。
//
// 用法：
//   lybox-mock                              // 默认端口 5173
//   lybox-mock --port 8080                  // 自定义端口
//   lybox-mock --wwwroot <path>             // 自定义 wwwroot 目录
//   lybox-mock --mock <path>                // 自定义 mock.json 路径
//   lybox-mock --plugin <pluginId>          // 默认 pluginId（用于日志提示）
//
// 端点：
//   GET  /__lybox/ipc.js         返回 ipc.js（供 HTML <script> 引入）
//   POST /__rpc                   Mock RPC，读 mock.json 返回假数据
//   GET  /sse/{pluginId}          Mock SSE，定时推送事件
//   POST /__emit                  浏览器模式事件 emit（返回 202）
//   POST /__channel/close         通道关闭（返回 202）
//   GET  /{pluginId}/{**path}     静态资源
//   GET  /                        健康检查

using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

var options = ParseArgs(args);

if (options.ShowHelp)
{
    PrintHelp();
    return 0;
}

// 解析 wwwroot
var wwwroot = ResolveWwwroot(options.Wwwroot, options.PluginId);
if (wwwroot is null)
{
    Console.WriteLine("[mock-server] 错误：找不到 wwwroot 目录");
    Console.WriteLine("[mock-server] 请通过 --wwwroot <path> 显式指定，或在仓库根目录运行");
    return 1;
}

var mockJsonPath = string.IsNullOrEmpty(options.MockJson)
    ? Path.Combine(wwwroot, ".lybox", "mock.json")
    : options.MockJson;

var ipcJsPath = ResolveIpcJs(wwwroot);

Console.WriteLine($"[mock-server] wwwroot: {wwwroot}");
Console.WriteLine($"[mock-server] mock.json: {mockJsonPath}");
Console.WriteLine($"[mock-server] ipc.js: {(string.IsNullOrEmpty(ipcJsPath) ? "(未找到)" : ipcJsPath)}");

// 加载 mock.json
JsonDocument mockData;
if (File.Exists(mockJsonPath))
{
    mockData = JsonDocument.Parse(File.ReadAllText(mockJsonPath));
}
else
{
    Console.WriteLine("[mock-server] 警告：mock.json 不存在，所有 RPC 调用将返回 null");
    mockData = JsonDocument.Parse("{}");
}

// 加载 ipc.js
var ipcJsContent = !string.IsNullOrEmpty(ipcJsPath) && File.Exists(ipcJsPath)
    ? File.ReadAllText(ipcJsPath)
    : "// ipc.js not found";

var urls = $"http://localhost:{options.Port}";

// 构建应用
var builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls(urls);
var app = builder.Build();

// 端点 1：ipc.js 注入
app.MapGet("/__lybox/ipc.js", () => Results.Content(ipcJsContent, "application/javascript; charset=utf-8"));

// 端点 2：Mock RPC
app.MapPost("/__rpc", async (HttpContext ctx) =>
{
    JsonDocument? body;
    try
    {
        body = await JsonSerializer.DeserializeAsync<JsonDocument>(
            ctx.Request.Body, (JsonSerializerOptions?)null, ctx.RequestAborted);
    }
    catch
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsync("""{"error":"请求体无效"}""", ctx.RequestAborted);
        return;
    }
    if (body is null)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var name = body.RootElement.GetProperty("name").GetString();
    Console.WriteLine($"[mock-server] RPC 调用: {name}");

    if (!mockData.RootElement.TryGetProperty(name!, out var entry))
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        await ctx.Response.WriteAsync($"{{\"error\":\"命令未在 mock.json 中定义: {name}\"}}", ctx.RequestAborted);
        return;
    }

    var delay = entry.TryGetProperty("delay", out var d) ? d.GetInt32() : 0;
    if (delay > 0) await Task.Delay(delay, ctx.RequestAborted);

    var resultJson = entry.TryGetProperty("result", out var r) ? r.GetRawText() : "null";
    ctx.Response.ContentType = "application/json; charset=utf-8";
    await ctx.Response.WriteAsync($"{{\"result\":{resultJson}}}", ctx.RequestAborted);
});

// 端点 3：Mock SSE
app.MapGet("/sse/{pluginId}", async (string pluginId, HttpContext ctx) =>
{
    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.Headers["Cache-Control"] = "no-cache";
    ctx.Response.Headers["Connection"] = "keep-alive";

    var readyPayload = $"{{\"pluginId\":\"{pluginId}\"}}";
    await ctx.Response.WriteAsync($"event: ready\ndata: {readyPayload}\n\n", ctx.RequestAborted);
    await ctx.Response.Body.FlushAsync(ctx.RequestAborted);

    var hasTick = false;
    JsonElement tickConfigSafe = default;
    var intervalMs = 2000;
    if (mockData.RootElement.TryGetProperty("_sseEvents", out var sse)
        && sse.TryGetProperty("tick", out var tick))
    {
        hasTick = true;
        tickConfigSafe = tick;
        if (tick.TryGetProperty("intervalMs", out var i)) intervalMs = i.GetInt32();
    }

    if (!hasTick)
    {
        try { await Task.Delay(Timeout.Infinite, ctx.RequestAborted); }
        catch { /* 客户端断开 */ }
        return;
    }

    var count = 0;
    var ct = ctx.RequestAborted;
    try
    {
        while (!ct.IsCancellationRequested)
        {
            count++;
            var now = DateTime.Now.ToString("HH:mm:ss");
            string payload;
            if (tickConfigSafe.TryGetProperty("data", out var d))
            {
                // 用运行时 count/time/message 覆盖静态值
                using var mem = new MemoryStream();
                using (var jw = new Utf8JsonWriter(mem))
                {
                    jw.WriteStartObject();
                    foreach (var prop in d.EnumerateObject())
                    {
                        if (prop.Name == "count") jw.WriteNumber("count", count);
                        else if (prop.Name == "time") jw.WriteString("time", now);
                        else if (prop.Name == "message") jw.WriteString("message", $"Mock 推送 #{count}（来自 lybox-mock）");
                        else prop.WriteTo(jw);
                    }
                    jw.WriteEndObject();
                }
                var dataJson = System.Text.Encoding.UTF8.GetString(mem.ToArray());
                payload = $"{{\"name\":\"tick\",\"data\":{dataJson}}}";
            }
            else
            {
                payload = $"{{\"name\":\"tick\",\"data\":{{\"count\":{count},\"time\":\"{now}\",\"message\":\"Mock 推送 #{count}\"}}}}";
            }
            await ctx.Response.WriteAsync($"event: dispatch\ndata: {payload}\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);
            await Task.Delay(intervalMs, ct);
        }
    }
    catch { /* 客户端断开 */ }
});

// 端点 4：事件 emit
app.MapPost("/__emit", (HttpContext ctx) =>
{
    ctx.Response.StatusCode = StatusCodes.Status202Accepted;
    return Task.CompletedTask;
});

// 端点 5：通道关闭
app.MapPost("/__channel/close", (HttpContext ctx) =>
{
    ctx.Response.StatusCode = StatusCodes.Status202Accepted;
    return Task.CompletedTask;
});

// 端点 6：静态资源
app.MapGet("/{pluginId}/{**path}", async (string pluginId, string path, HttpContext ctx) =>
{
    var fullPath = Path.GetFullPath(Path.Combine(wwwroot, path.Replace('/', Path.DirectorySeparatorChar)));
    var rootFull = Path.GetFullPath(wwwroot);
    if (!fullPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
    }
    else if (!File.Exists(fullPath))
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
    }
    else
    {
        ctx.Response.ContentType = GuessMimeType(fullPath);
        await ctx.Response.SendFileAsync(fullPath, ctx.RequestAborted);
    }
});

// 端点 7：健康检查
app.MapGet("/", () => Results.Ok(new { service = "LYBox.MockServer", port = options.Port, wwwroot }));

Console.WriteLine($"[mock-server] 启动中: {urls}");
Console.WriteLine($"[mock-server] 前端入口: {urls}/{options.PluginId}/index.html");
Console.WriteLine($"[mock-server] 按 Ctrl+C 停止");

app.Run();
return 0;

// —— 辅助函数 ——
static Options ParseArgs(string[] args)
{
    var opts = new Options();
    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--port" when i + 1 < args.Length:
                opts.Port = int.Parse(args[++i]);
                break;
            case "--wwwroot" when i + 1 < args.Length:
                opts.Wwwroot = args[++i];
                break;
            case "--mock" when i + 1 < args.Length:
                opts.MockJson = args[++i];
                break;
            case "--plugin" when i + 1 < args.Length:
                opts.PluginId = args[++i];
                break;
            case "--help":
            case "-h":
                opts.ShowHelp = true;
                break;
        }
    }
    return opts;
}

static void PrintHelp()
{
    Console.WriteLine("""
    LYBox Mock Server —— 前端调试 Mock 后端

    用法:
      lybox-mock [options]

    选项:
      --port <port>           监听端口（默认 5173）
      --wwwroot <path>        插件 wwwroot 目录（默认自动检测）
      --mock <path>           mock.json 路径（默认 <wwwroot>/.lybox/mock.json）
      --plugin <pluginId>     默认 pluginId（仅用于日志提示，默认 WebTemplate）
      --help, -h              显示帮助

    端点:
      GET  /__lybox/ipc.js     返回 ipc.js
      POST /__rpc              Mock RPC
      GET  /sse/{pluginId}     Mock SSE
      GET  /{pluginId}/{path}  静态资源

    示例:
      lybox-mock
      lybox-mock --port 8080 --wwwroot ./plugins/LYBox.Plugin.WebTemplate/wwwroot
    """);
}

static string? ResolveWwwroot(string explicitPath, string pluginId)
{
    if (!string.IsNullOrEmpty(explicitPath) && Directory.Exists(explicitPath))
        return explicitPath;

    // 向上遍历查找仓库根（标志：存在 plugins/ 目录）
    var dir = new DirectoryInfo(Environment.CurrentDirectory);
    for (var i = 0; i < 6 && dir is not null; i++)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "plugins")))
        {
            var candidate = Path.Combine(dir.FullName, "plugins", "LYBox.Plugin.WebTemplate", "wwwroot");
            if (Directory.Exists(candidate)) return candidate;
        }
        dir = dir.Parent;
    }

    return Directory.Exists("wwwroot") ? Path.GetFullPath("wwwroot") : null;
}

static string? ResolveIpcJs(string wwwroot)
{
    var dir = new DirectoryInfo(wwwroot);
    for (var i = 0; i < 6 && dir is not null; i++)
    {
        var p = Path.Combine(dir.FullName, "src", "Plugin", "LYBox.Plugin.Shared", "Rpc", "Assets", "ipc.js");
        if (File.Exists(p)) return p;
        dir = dir.Parent;
    }
    return null;
}

static string GuessMimeType(string filePath)
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
        ".map" => "application/json; charset=utf-8",
        ".webp" => "image/webp",
        ".wasm" => "application/wasm",
        _ => "application/octet-stream",
    };
}

class Options
{
    public int Port { get; set; } = 5173;
    public string Wwwroot { get; set; } = "";
    public string MockJson { get; set; } = "";
    public string PluginId { get; set; } = "8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d";
    public bool ShowHelp { get; set; }
}
