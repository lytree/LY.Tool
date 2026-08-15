using System.Text.Json;

namespace LYBox.Plugin.Shared.Rpc;

/// <summary>
/// 流式通道（借鉴 Tauri Channel，弥补 Wails 无原生流式）。
/// C# 侧创建并拥有，通过 <see cref="IRpcHost.CreateChannel{T}"/> 取得。
/// 每次 <see cref="WriteAsync"/> 把数据推送到前端
/// （经 <c>window.__lybox.channel.onData(id, json)</c> 或 SSE <c>channel-data</c> 事件）。
/// 单订阅、自动取消：<see cref="Dispose"/> 时通知前端 onClose。
/// </summary>
/// <remarks>
/// 注入 <see cref="IEventPusher"/> 后，<see cref="WriteAsync"/> / <see cref="CloseAsync"/>
/// 优先走 SSE（<c>channel-data</c> / <c>channel-close</c> 事件类型），避免高频推送时 InvokeScript 队列堆积；
/// 未注入时降级到 <c>InvokeScript</c>，向后兼容。
/// </remarks>
public sealed class Channel<T> : Channel, IAsyncDisposable
{
    private readonly IRpcTransport _transport;
    private readonly IEventPusher? _eventPusher;
    private readonly string? _pluginId;
    private readonly CancellationTokenSource _cts = new();

    internal Channel(string id, IRpcTransport transport, IEventPusher? eventPusher = null, string? pluginId = null) : base(id)
    {
        _transport = transport;
        _eventPusher = eventPusher;
        _pluginId = pluginId;
    }

    /// <summary>向前端推送一条数据。</summary>
    public async ValueTask WriteAsync(T item, CancellationToken cancellationToken = default)
    {
        if (_cts.IsCancellationRequested) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        var idJson = JsonSerializer.Serialize(Id, RpcEnvelope.JsonOptions);
        var dataJson = JsonSerializer.Serialize(item, RpcEnvelope.JsonOptions);

        // 优先走 SSE（高频推送场景）
        if (_eventPusher is not null && _pluginId is not null)
        {
            // SSE 负载格式：{"id":"...","data":...}
            var payload = $"{{\"id\":{idJson},\"data\":{dataJson}}}";
            try { await _eventPusher.PushAsync(_pluginId, "channel-data", payload, linked.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            return;
        }

        var js = $"window.__lybox.channel.onData({idJson},{dataJson});";
        try { await _transport.ExecuteScriptAsync(js, linked.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
    }

    /// <summary>关闭通道并通知前端。</summary>
    public override async ValueTask CloseAsync()
    {
        if (Closed) return;
        _cts.Cancel();
        var idJson = JsonSerializer.Serialize(Id, RpcEnvelope.JsonOptions);

        // 优先走 SSE
        if (_eventPusher is not null && _pluginId is not null)
        {
            var payload = $"{{\"id\":{idJson}}}";
            try { await _eventPusher.PushAsync(_pluginId, "channel-close", payload, CancellationToken.None).ConfigureAwait(false); }
            catch { }
            MarkClosed();
            return;
        }

        var js = $"window.__lybox.channel.onClose({idJson});";
        try { await _transport.ExecuteScriptAsync(js, CancellationToken.None).ConfigureAwait(false); }
        catch { }
        MarkClosed();
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _cts.Dispose();
    }
}

/// <summary>非泛型基类。</summary>
public abstract class Channel
{
    /// <summary>通道唯一标识，前端凭此订阅。</summary>
    public string Id { get; }
    /// <summary>是否已关闭。</summary>
    public bool Closed { get; private set; }

    protected Channel(string id) => Id = id;

    protected void MarkClosed() => Closed = true;

    /// <summary>关闭通道（派生类实现）。</summary>
    public abstract ValueTask CloseAsync();
}
