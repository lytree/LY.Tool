using System.Collections.Concurrent;
using System.Text.Json;

namespace LYBox.Plugin.Shared.Rpc;

/// <summary>Single command and generated-client registry shared by WebView and HTTP transports.</summary>
public sealed class PluginRpcDispatcher
{
    private readonly ConcurrentDictionary<RpcRoute, RpcRegistration> _commands = new();
    private readonly ConcurrentDictionary<ArtifactRoute, RpcClientArtifact> _artifacts = new();

    public void RegisterPayload(string pluginId, string method, RpcPayloadCommandHandler handler)
    {
        ValidateRoute(pluginId, method);
        ArgumentNullException.ThrowIfNull(handler);
        _commands[new RpcRoute(pluginId, method)] = RpcRegistration.ForPayload(handler);
    }

    public void RegisterLegacy(string pluginId, string method, RpcCommandHandler handler)
    {
        ValidateRoute(pluginId, method);
        ArgumentNullException.ThrowIfNull(handler);
        _commands[new RpcRoute(pluginId, method)] = RpcRegistration.ForLegacy(handler);
    }

    public void RegisterArtifact(string pluginId, RpcClientArtifact artifact)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(artifact);
        if (string.IsNullOrWhiteSpace(artifact.Name)
            || artifact.Name.Contains('/')
            || artifact.Name.Contains('\\'))
        {
            throw new ArgumentException("Generated client artifact name must be a file name.", nameof(artifact));
        }

        _artifacts[new ArtifactRoute(pluginId, artifact.Name)] = artifact;
    }

    public bool TryGetArtifact(string pluginId, string name, out RpcClientArtifact artifact) =>
        _artifacts.TryGetValue(new ArtifactRoute(pluginId, name), out artifact!);

    public IReadOnlyCollection<string> GetMethods(string? pluginId = null) =>
        _commands.Keys
            .Where(route => pluginId is null || string.Equals(route.PluginId, pluginId, StringComparison.Ordinal))
            .Select(route => pluginId is null ? $"{route.PluginId}:{route.Method}" : route.Method)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

    public async Task<PluginRpcResult> InvokePayloadAsync(
        PluginRpcCall call,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(call.Id)
            || string.IsNullOrWhiteSpace(call.PluginId)
            || string.IsNullOrWhiteSpace(call.Method))
        {
            return Failure(call.Id, PluginRpcErrorCodes.InvalidRequest, "RPC request is incomplete.", call.TraceId);
        }

        if (!_commands.TryGetValue(new RpcRoute(call.PluginId, call.Method), out var registration))
            return Failure(call.Id, PluginRpcErrorCodes.MethodNotFound, "RPC method was not found.", call.TraceId);

        if (registration.PayloadHandler is null)
        {
            return Failure(
                call.Id,
                PluginRpcErrorCodes.InvalidPayload,
                "This legacy RPC method requires positional arguments.",
                call.TraceId);
        }

        return await InvokeCoreAsync(
            call,
            token => registration.PayloadHandler(call.Payload, token),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<PluginRpcResult> InvokeLegacyAsync(
        string requestId,
        string pluginId,
        string method,
        JsonElement[] args,
        CancellationToken cancellationToken = default)
    {
        if (!_commands.TryGetValue(new RpcRoute(pluginId, method), out var registration))
            return Failure(requestId, PluginRpcErrorCodes.MethodNotFound, "RPC method was not found.");

        if (registration.LegacyHandler is not null)
        {
            var call = new PluginRpcCall(requestId, pluginId, method, RpcJson.Null);
            return await InvokeCoreAsync(
                call,
                token => registration.LegacyHandler(args, token),
                cancellationToken).ConfigureAwait(false);
        }

        var payload = args.Length switch
        {
            0 => RpcJson.Null,
            1 => args[0],
            _ => default,
        };
        if (args.Length > 1)
        {
            return Failure(
                requestId,
                PluginRpcErrorCodes.InvalidPayload,
                "Canonical RPC methods accept one payload object.");
        }

        var canonicalCall = new PluginRpcCall(requestId, pluginId, method, payload);
        return await InvokeCoreAsync(
            canonicalCall,
            token => registration.PayloadHandler!(payload, token),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<PluginRpcResult> InvokeCoreAsync(
        PluginRpcCall call,
        Func<CancellationToken, Task<object?>> invoke,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = await invoke(cancellationToken).ConfigureAwait(false);
            return new PluginRpcResult(call.Id, true, payload);
        }
        catch (PluginRpcException exception)
        {
            return Failure(call.Id, exception.Code, exception.Message, call.TraceId, exception.Details);
        }
        catch (JsonException exception)
        {
            return Failure(
                call.Id,
                PluginRpcErrorCodes.InvalidPayload,
                "RPC payload is invalid.",
                call.TraceId,
                new { exception.Path });
        }
        catch (OperationCanceledException)
        {
            return Failure(call.Id, PluginRpcErrorCodes.Cancelled, "RPC call was cancelled.", call.TraceId);
        }
        catch (Exception)
        {
            var traceId = call.TraceId ?? Guid.NewGuid().ToString("N");
            return Failure(
                call.Id,
                PluginRpcErrorCodes.HandlerError,
                "RPC handler failed.",
                traceId);
        }
    }

    private static PluginRpcResult Failure(
        string? id,
        string code,
        string message,
        string? traceId = null,
        object? details = null) =>
        new(id ?? string.Empty, false, Error: new PluginRpcError(code, message, details, traceId));

    private static void ValidateRoute(string pluginId, string method)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
    }

    private readonly record struct RpcRoute(string PluginId, string Method);

    private readonly record struct ArtifactRoute(string PluginId, string Name);

    private sealed record RpcRegistration(
        RpcPayloadCommandHandler? PayloadHandler,
        RpcCommandHandler? LegacyHandler)
    {
        public static RpcRegistration ForPayload(RpcPayloadCommandHandler handler) => new(handler, null);

        public static RpcRegistration ForLegacy(RpcCommandHandler handler) => new(null, handler);
    }
}
