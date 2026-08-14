using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using LYBox.Plugin.Shared.Web;

namespace LYBox.Plugin.Shared.Rpc;

/// <summary>
/// IPC 运行时主机（transport-agnostic）。实现 <see cref="IRpcHost"/>，
/// 在 <see cref="IRpcTransport.MessageReceived"/> 上按 Wails v2 前缀信封分发：
/// <c>C</c> 调用、<c>E</c> 事件、<c>X</c> 通道关闭。结果经
/// <c>window.__lybox.resolve</c> 回推 Promise（弥补 Avalonia WebView 的 fire-and-forget 缺陷）。
/// </summary>
/// <remarks>
/// <para>
/// C# → JS 推送有两套通道：
/// <list type="bullet">
/// <item><c>InvokeScript</c>：RPC resolve 同步回推（必须，因 Promise 需要返回值）。事件分发 / 通道数据降级走此通道。</item>
/// <item>SSE（<see cref="IEventPusher"/>）：高频推送通道。注入后 <see cref="EmitEventAsync"/> 与 <see cref="Channel{T}"/>.WriteAsync 优先走此通道。</item>
/// </list>
/// </para>
/// <para>
/// 构造时可选注入 <paramref name="eventPusher"/> + <paramref name="pluginId"/> 启用 SSE 推送；
/// 不注入则保持原有 <c>InvokeScript</c> 行为，向后兼容。
/// </para>
/// </remarks>
public sealed class WebViewIpcHost : IRpcHost
{
    private readonly IRpcTransport _transport;
    private readonly IEventPusher? _eventPusher;
    private readonly string? _pluginId;
    private readonly WebHostService? _webHost;
    private readonly ConcurrentDictionary<string, RpcCommandHandler> _commands = new();
    private readonly ConcurrentDictionary<string, Channel> _channels = new();
    private readonly ConcurrentDictionary<string, List<Action<JsonElement?>>> _eventListeners = new();
    private readonly TaskCompletionSource _readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string _bootstrapJs;
    private bool _bootstrapInjected;

    private const string ReadyEvent = "__lybox:ready";

    /// <param name="transport">底层双向传输。</param>
    /// <param name="eventPusher">可选 SSE 推送器。注入后事件分发 / 通道数据优先走 SSE，避免 InvokeScript 队列堆积。</param>
    /// <param name="pluginId">与 SSE 路由 <c>/sse/{pluginId}</c> 对应的插件 ID。<paramref name="eventPusher"/> 非 null 时必须提供。</param>
    /// <param name="webHost">可选 WebHostService。注入后命令同步注册到 HTTP RPC 桥（POST /__rpc），支持浏览器模式调试。</param>
    public WebViewIpcHost(IRpcTransport transport, IEventPusher? eventPusher = null, string? pluginId = null, WebHostService? webHost = null)
    {
        _transport = transport;
        _eventPusher = eventPusher;
        _pluginId = pluginId;
        _webHost = webHost;
        _transport.MessageReceived += OnMessage;
        _bootstrapJs = LoadBootstrap();
    }

    /// <summary>前端 __lybox 运行时就绪（握手完成）。注入引导脚本后等待此任务完成。</summary>
    public Task WhenReady => _readyTcs.Task;

    /// <summary>注入 ipc.js 引导脚本到页面。幂等。</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_bootstrapInjected) return;
        await _transport.ExecuteScriptAsync(_bootstrapJs, cancellationToken);
        _bootstrapInjected = true;
    }

    /// <summary>
    /// 把已注册命令的清单注入页面。调用前应完成所有 RegisterCommand。
    /// 当前实现：manifest 仅含命令短名列表，供调试面板展示。前端调用统一走 <c>window.__lybox.rpc(name, args)</c>，无需构建 window.go。
    /// </summary>
    public async Task InjectBindingsAsync(CancellationToken cancellationToken = default)
    {
        var manifest = _commands.Keys.Select(name => new { name });
        var json = JsonSerializer.Serialize(manifest, RpcEnvelope.JsonOptions);
        // JS 端 setBindings 已改为 noop（仅保存 manifest 供调试工具读取）
        var js = $"window.__lybox && window.__lybox.setBindings({JsonSerializer.Serialize(json)});";
        await _transport.ExecuteScriptAsync(js, cancellationToken);
    }

    /// <summary>
    /// 注册一个 RPC 命令。命令名建议用短名（如 <c>GreetAsync</c>），与前端 <c>window.__lybox.rpc(name, ...)</c> 的 name 一致。
    /// 若注入了 <see cref="WebHostService"/>，命令同步注册到 HTTP RPC 桥（POST /__rpc），支持浏览器模式调试。
    /// </summary>
    public void RegisterCommand(string name, RpcCommandHandler handler)
    {
        _commands[name] = handler;
        _webHost?.RegisterRpcHandler(name, handler);
    }

    public async Task EmitEventAsync(string name, object? data, CancellationToken cancellationToken = default)
    {
        var nameJson = JsonSerializer.Serialize(name, RpcEnvelope.JsonOptions);
        var dataJson = data is null ? "null" : JsonSerializer.Serialize(data, RpcEnvelope.JsonOptions);

        // 优先走 SSE（高频推送场景），无 pusher 时降级到 InvokeScript
        if (_eventPusher is not null && _pluginId is not null)
        {
            // SSE 负载格式：{"name":"...","data":...}
            var payload = $"{{\"name\":{nameJson},\"data\":{dataJson}}}";
            await _eventPusher.PushAsync(_pluginId, "dispatch", payload, cancellationToken).ConfigureAwait(false);
            return;
        }

        var js = $"window.__lybox && window.__lybox.dispatch({nameJson},{dataJson});";
        await _transport.ExecuteScriptAsync(js, cancellationToken).ConfigureAwait(false);
    }

    public Channel<T> CreateChannel<T>(string? id = null)
    {
        var cid = id ?? Guid.NewGuid().ToString("N");
        var ch = new Channel<T>(cid, _transport, _eventPusher, _pluginId);
        _channels[cid] = ch;
        return ch;
    }

    /// <summary>订阅来自前端的事件（JS 经 __lybox.emit 发送）。</summary>
    public Action OnEvent(string name, Action<JsonElement?> handler)
    {
        var list = _eventListeners.GetOrAdd(name, _ => new List<Action<JsonElement?>>());
        lock (list) list.Add(handler);
        return () => { lock (list) list.Remove(handler); };
    }

    private void OnMessage(string? body)
    {
        if (string.IsNullOrEmpty(body)) return;
        var prefix = body[0];
        var payload = body.Substring(1);
        switch (prefix)
        {
            case RpcEnvelope.PrefixCall:
                _ = HandleCallAsync(payload);
                break;
            case RpcEnvelope.PrefixEvent:
                HandleEvent(payload);
                break;
            case RpcEnvelope.PrefixChannelClose:
                HandleChannelClose(payload);
                break;
        }
    }

    private async Task HandleCallAsync(string payload)
    {
        CallMessage msg;
        try { msg = JsonSerializer.Deserialize<CallMessage>(payload, RpcEnvelope.JsonOptions)!; }
        catch (Exception) { return; /* 无法解析的调用，丢弃 */ }

        if (msg.Name == ReadyEvent) return; // 握手由事件路径处理

        if (!_commands.TryGetValue(msg.Name, out var handler))
        {
            await ResolveAsync(msg.CallbackId, $"命令未注册: {msg.Name}", null);
            return;
        }

        try
        {
            var result = await handler(msg.Args, CancellationToken.None);
            if (result is Channel ch)
            {
                var itemType = ch.GetType().IsGenericType
                    ? ch.GetType().GetGenericArguments()[0].Name
                    : "any";
                var descriptor = new { __channel = true, id = ch.Id, itemType };
                await ResolveAsync(msg.CallbackId, null, descriptor);
            }
            else
            {
                await ResolveAsync(msg.CallbackId, null, result);
            }
        }
        catch (Exception ex)
        {
            await ResolveAsync(msg.CallbackId, ex.Message, null);
        }
    }

    private void HandleEvent(string payload)
    {
        EventMessage? msg;
        try { msg = JsonSerializer.Deserialize<EventMessage>(payload, RpcEnvelope.JsonOptions); }
        catch { return; }
        if (msg is null) return;

        if (msg.Name == ReadyEvent)
        {
            _readyTcs.TrySetResult();
            return;
        }

        if (_eventListeners.TryGetValue(msg.Name, out var list))
        {
            List<Action<JsonElement?>> snapshot;
            lock (list) snapshot = list.ToList();
            foreach (var cb in snapshot)
            {
                try { cb(msg.Data); } catch { /* 单个监听器异常不影响其他 */ }
            }
        }
    }

    private void HandleChannelClose(string channelId)
    {
        if (_channels.TryRemove(channelId, out var ch))
        {
            _ = ch.CloseAsync();
        }
    }

    private async Task ResolveAsync(string callbackId, string? err, object? result)
    {
        var errJson = err is null ? "null" : JsonSerializer.Serialize(err, RpcEnvelope.JsonOptions);
        var resultJson = result is null ? "null" : JsonSerializer.Serialize(result, RpcEnvelope.JsonOptions);
        var js = $"window.__lybox && window.__lybox.resolve({JsonSerializer.Serialize(callbackId)},{errJson},{resultJson});";
        try { await _transport.ExecuteScriptAsync(js); }
        catch { /* 页面已销毁等，忽略 */ }
    }

    private static string LoadBootstrap()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("ipc.js", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("找不到嵌入式 ipc.js 资源");
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}