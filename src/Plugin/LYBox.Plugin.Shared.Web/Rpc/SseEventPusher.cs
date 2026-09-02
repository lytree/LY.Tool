using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace LYBox.Plugin.Shared.Rpc;

/// <summary>Fan-out pusher backed by one bounded queue and writer loop per SSE client.</summary>
public sealed class SseEventPusher : IEventPusher
{
    private readonly ConcurrentDictionary<string, ClientList> _clients = new();

    public void Subscribe(string pluginId, SseClient client)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(client);
        var list = _clients.GetOrAdd(pluginId, _ => new ClientList());
        lock (list.Lock)
            list.Clients.Add(client);
    }

    public void Unsubscribe(string pluginId, SseClient client)
    {
        if (!_clients.TryGetValue(pluginId, out var list)) return;
        lock (list.Lock)
            list.Clients.Remove(client);
    }

    public Task PushAsync(
        string pluginId,
        string eventType,
        string json,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_clients.TryGetValue(pluginId, out var list))
            return Task.CompletedTask;

        List<SseClient> snapshot;
        lock (list.Lock)
            snapshot = list.Clients.ToList();

        foreach (var client in snapshot)
            client.TryEnqueue(eventType, json);

        return Task.CompletedTask;
    }

    public int GetSubscriberCount(string pluginId)
    {
        if (!_clients.TryGetValue(pluginId, out var list)) return 0;
        lock (list.Lock)
            return list.Clients.Count;
    }

    public void Clear()
    {
        foreach (var pair in _clients)
        {
            List<SseClient> snapshot;
            lock (pair.Value.Lock)
            {
                snapshot = pair.Value.Clients.ToList();
                pair.Value.Clients.Clear();
            }

            foreach (var client in snapshot)
                client.Dispose();
        }
        _clients.Clear();
    }

    private sealed class ClientList
    {
        public List<SseClient> Clients { get; } = new();
        public object Lock { get; } = new();
    }
}

/// <summary>Bounded SSE client queue. Events drop oldest; channels fail explicitly on overflow.</summary>
public sealed class SseClient : IDisposable, IAsyncDisposable
{
    public const int DefaultMaxFrames = 256;
    public const int DefaultMaxBytes = 1024 * 1024;

    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);
    private readonly Stream _stream;
    private readonly int _maxFrames;
    private readonly int _maxBytes;
    private readonly Queue<byte[]> _queue = new();
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly CancellationTokenSource _lifetime;
    private readonly Task _writer;
    private readonly object _gate = new();
    private int _queuedBytes;
    private bool _completeAfterDrain;
    private bool _disposed;

    public SseClient(
        Stream stream,
        CancellationToken cancellationToken = default,
        int maxFrames = DefaultMaxFrames,
        int maxBytes = DefaultMaxBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (maxFrames < 2) throw new ArgumentOutOfRangeException(nameof(maxFrames));
        if (maxBytes < 1024) throw new ArgumentOutOfRangeException(nameof(maxBytes));

        _stream = stream;
        _maxFrames = maxFrames;
        _maxBytes = maxBytes;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _writer = RunWriterAsync();
    }

    public Task Completion => _writer;

    public bool TryEnqueue(string eventType, string json)
    {
        var frame = Encode(eventType, json);
        lock (_gate)
        {
            if (_disposed || _completeAfterDrain) return false;

            if (string.Equals(eventType, "channel-data", StringComparison.Ordinal)
                && WouldOverflow(frame.Length))
            {
                FailSlowChannel(json);
                SignalWriter();
                return false;
            }

            while (_queue.Count > 0 && WouldOverflow(frame.Length))
                RemoveOldest();

            if (frame.Length > _maxBytes)
                return false;

            Enqueue(frame);
            SignalWriter();
            return true;
        }
    }

    private async Task RunWriterAsync()
    {
        var cancellationToken = _lifetime.Token;
        try
        {
            while (true)
            {
                byte[]? frame = null;
                var complete = false;
                lock (_gate)
                {
                    if (_queue.Count > 0)
                    {
                        frame = _queue.Dequeue();
                        _queuedBytes -= frame.Length;
                    }
                    else
                    {
                        complete = _completeAfterDrain;
                    }
                }

                if (frame is not null)
                {
                    await _stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
                    await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (complete) break;

                if (!await _signal.WaitAsync(KeepAliveInterval, cancellationToken).ConfigureAwait(false))
                {
                    var keepAlive = Encoding.UTF8.GetBytes(": keep-alive\n\n");
                    await _stream.WriteAsync(keepAlive, cancellationToken).ConfigureAwait(false);
                    await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
        }
        finally
        {
            lock (_gate)
                _disposed = true;
        }
    }

    private void FailSlowChannel(string channelJson)
    {
        var channelId = ReadChannelId(channelJson);
        _queue.Clear();
        _queuedBytes = 0;

        var error = JsonSerializer.Serialize(
            new
            {
                id = channelId,
                error = new PluginRpcError(
                    PluginRpcErrorCodes.SlowConsumer,
                    "Channel closed because the client could not keep up."),
            },
            RpcEnvelope.JsonOptions);
        Enqueue(Encode("channel-error", error));
        Enqueue(Encode("channel-close", JsonSerializer.Serialize(new { id = channelId }, RpcEnvelope.JsonOptions)));
        _completeAfterDrain = true;
    }

    private bool WouldOverflow(int nextBytes) =>
        _queue.Count >= _maxFrames || _queuedBytes + nextBytes > _maxBytes;

    private void Enqueue(byte[] frame)
    {
        _queue.Enqueue(frame);
        _queuedBytes += frame.Length;
    }

    private void RemoveOldest()
    {
        var removed = _queue.Dequeue();
        _queuedBytes -= removed.Length;
    }

    private void SignalWriter()
    {
        if (_signal.CurrentCount == 0)
            _signal.Release();
    }

    private static byte[] Encode(string eventType, string json) =>
        Encoding.UTF8.GetBytes($"event: {eventType}\ndata: {json}\n\n");

    private static string ReadChannelId(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("id", out var id)
                ? id.GetString() ?? string.Empty
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _lifetime.Cancel();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await _writer.ConfigureAwait(false);
        _signal.Dispose();
        _lifetime.Dispose();
    }
}
