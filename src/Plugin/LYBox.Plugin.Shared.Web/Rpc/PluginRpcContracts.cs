using System.Text.Json;
using System.Text.Json.Serialization;

namespace LYBox.Plugin.Shared.Rpc;

public static class PluginRpcErrorCodes
{
    public const string InvalidRequest = "invalid_request";
    public const string InvalidPayload = "invalid_payload";
    public const string MethodNotFound = "method_not_found";
    public const string PluginMismatch = "plugin_mismatch";
    public const string Cancelled = "cancelled";
    public const string Timeout = "timeout";
    public const string Busy = "busy";
    public const string HandlerError = "handler_error";
    public const string TransportError = "transport_error";
    public const string ChannelClosed = "channel_closed";
    public const string SlowConsumer = "slow_consumer";
}

public sealed record PluginRpcError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("details")] object? Details = null,
    [property: JsonPropertyName("traceId")] string? TraceId = null);

public sealed record PluginRpcCall(
    string Id,
    string PluginId,
    string Method,
    JsonElement Payload,
    string? TraceId = null);

public sealed record PluginRpcResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("payload")] object? Payload = null,
    [property: JsonPropertyName("error")] PluginRpcError? Error = null)
{
    [JsonPropertyName("version")]
    public int Version => 2;

    [JsonPropertyName("kind")]
    public string Kind => "plugin-rpc-result";
}

public sealed record RpcClientArtifact(string Name, string ContentType, string Content)
{
    public const string JavaScriptContentType = "text/javascript; charset=utf-8";
    public const string TypeScriptContentType = "text/plain; charset=utf-8";
}

public sealed class PluginRpcException : Exception
{
    public PluginRpcException(string code, string message, object? details = null)
        : base(message)
    {
        Code = code;
        Details = details;
    }

    public string Code { get; }

    public object? Details { get; }
}

internal static class RpcJson
{
    private static readonly JsonElement NullElement = CreateNull();

    public static JsonElement Null => NullElement;

    private static JsonElement CreateNull()
    {
        using var document = JsonDocument.Parse("null");
        return document.RootElement.Clone();
    }
}
