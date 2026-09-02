using System.Text.Json;

namespace LYBox.Plugin.Shared.Rpc;

/// <summary>RPC 命令处理器：接收已反序列化的参数数组，返回任意可 JSON 序列化的对象。
/// 若返回值为 <see cref="Channel"/> 实例，主机将其作为流式通道处理（resolve 的结果是通道描述符）。</summary>
public delegate Task<object?> RpcCommandHandler(JsonElement[] args, CancellationToken cancellationToken);

/// <summary>Canonical RPC handler: one named JSON payload plus cancellation.</summary>
public delegate Task<object?> RpcPayloadCommandHandler(JsonElement payload, CancellationToken cancellationToken);

/// <summary>
/// RPC 主机：管理命令注册、事件双向分发、流式通道。
/// 由 <see cref="WebViewIpcHost"/> 实现；插件/宿主通过此接口注册命令。
/// </summary>
public interface IRpcHost
{
    /// <summary>注册一个命令。命令名建议为 "Namespace.Class.Method" 形式。</summary>
    void RegisterCommand(string name, RpcCommandHandler handler);

    /// <summary>向前端分发事件（C#→JS）。</summary>
    Task EmitEventAsync(string name, object? data, CancellationToken cancellationToken = default);

    /// <summary>创建一个 C# 侧拥有的流式通道，向前端推送数据。前端通过返回的 <see cref="Channel.Id"/> 订阅。</summary>
    Channel<T> CreateChannel<T>(string? id = null);
}

/// <summary>Canonical capabilities implemented by the LYBox host.</summary>
public interface ICanonicalRpcHost
{
    void RegisterPayloadCommand(string name, RpcPayloadCommandHandler handler);

    void RegisterClientArtifact(RpcClientArtifact artifact);
}

/// <summary>Compatibility helpers used by generated bindings.</summary>
public static class RpcHostExtensions
{
    public static void RegisterCanonicalCommand(
        this IRpcHost host,
        string name,
        RpcPayloadCommandHandler handler)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);

        if (host is ICanonicalRpcHost canonical)
        {
            canonical.RegisterPayloadCommand(name, handler);
            return;
        }

        host.RegisterCommand(name, async (args, cancellationToken) =>
        {
            var payload = args.Length switch
            {
                0 => RpcJson.Null,
                1 => args[0],
                _ => throw new PluginRpcException(
                    PluginRpcErrorCodes.InvalidPayload,
                    $"RPC method '{name}' accepts one payload object."),
            };
            return await handler(payload, cancellationToken).ConfigureAwait(false);
        });
    }

    public static void RegisterGeneratedClientArtifact(this IRpcHost host, RpcClientArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(artifact);
        if (host is ICanonicalRpcHost canonical)
            canonical.RegisterClientArtifact(artifact);
    }
}

/// <summary>由源生成器针对每个含 [RpcCommand] 的类生成实现。
/// 宿主调用 <see cref="RegisterBindings"/> 把该类所有命令注册到 <see cref="IRpcHost"/>。</summary>
public interface IRpcBindingSource
{
    /// <summary>把当前类所有 [RpcCommand] 方法注册到主机。</summary>
    void RegisterBindings(IRpcHost host);
}
