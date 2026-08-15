using System.Collections.Concurrent;
using System.Text;

namespace LYBox.Plugin.Shared.Rpc;

/// <summary>
/// <see cref="IEventPusher"/> 的默认实现：在内存中维护每个 pluginId 的 SSE 客户端列表，
/// <see cref="PushAsync"/> 时遍历所有客户端把消息写入对应的 HTTP 响应流。
/// </summary>
/// <remarks>
/// 线程安全：<see cref="_clients"/> 使用 <see cref="ConcurrentDictionary{TKey, TValue}"/>，
/// 每个 pluginId 的客户端列表使用 <see cref="List{T}"/> + lock 保护订阅 / 退订 / 遍历。
/// 客户端连接断开时（<see cref="SseClient.WriteAsync"/> 抛异常）自动从列表移除。
/// </remarks>
public sealed class SseEventPusher : IEventPusher
{
    private readonly ConcurrentDictionary<string, ClientList> _clients = new();

    /// <summary>
    /// 订阅：把一个 SSE 客户端加入 pluginId 的分发列表。
    /// 由 <c>WebHostService</c> 在 SSE 请求处理中调用。
    /// </summary>
    public void Subscribe(string pluginId, SseClient client)
    {
        var list = _clients.GetOrAdd(pluginId, _ => new ClientList());
        lock (list.Lock) list.Clients.Add(client);
    }

    /// <summary>
    /// 退订：连接断开时调用，从分发列表移除客户端。
    /// </summary>
    public void Unsubscribe(string pluginId, SseClient client)
    {
        if (!_clients.TryGetValue(pluginId, out var list)) return;
        lock (list.Lock) list.Clients.Remove(client);
    }

    /// <inheritdoc />
    public async Task PushAsync(string pluginId, string eventType, string json, CancellationToken cancellationToken = default)
    {
        if (!_clients.TryGetValue(pluginId, out var list) || list.Clients.Count == 0) return;

        // SSE 消息格式：event: <type>\ndata: <json>\n\n
        var message = $"event: {eventType}\ndata: {json}\n\n";
        var bytes = Encoding.UTF8.GetBytes(message);

        List<SseClient> snapshot;
        lock (list.Lock) snapshot = list.Clients.ToList();

        List<SseClient>? dead = null;
        foreach (var c in snapshot)
        {
            try
            {
                await c.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // 客户端连接已断开 / 写入失败，加入待移除列表
                dead ??= new List<SseClient>();
                dead.Add(c);
            }
        }

        if (dead is not null)
        {
            lock (list.Lock)
            {
                foreach (var c in dead) list.Clients.Remove(c);
            }
        }
    }

    /// <summary>获取指定插件的当前订阅客户端数（主要供测试断言使用）。</summary>
    public int GetSubscriberCount(string pluginId)
        => _clients.TryGetValue(pluginId, out var list) ? list.Clients.Count : 0;

    private sealed class ClientList
    {
        public List<SseClient> Clients { get; } = new();
        public object Lock { get; } = new();
    }
}

/// <summary>
/// 单个 SSE 客户端：包装 HTTP 响应流，提供异步写入消息的方法。
/// 由 <c>WebHostService</c> 在处理 <c>GET /sse/{pluginId}</c> 时创建，
/// 把 <see cref="SseEventPusher.PushAsync"/> 写出的 SSE 字节透传给浏览器。
/// </summary>
public sealed class SseClient
{
    private readonly Stream _stream;

    public SseClient(Stream stream) => _stream = stream;

    /// <summary>写入一条完整的 SSE 消息（已编码为 UTF-8 字节）。</summary>
    public async Task WriteAsync(byte[] bytes, CancellationToken cancellationToken = default)
    {
        await _stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
