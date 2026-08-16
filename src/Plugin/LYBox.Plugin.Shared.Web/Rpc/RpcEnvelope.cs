using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LYBox.Plugin.Shared.Rpc;

/// <summary>
/// 消息信封：前缀字符 + JSON 负载。复刻 Wails v2 的单字符串通道模型，
/// 并在此基础上扩展 Channel 流式通道（弥补 Wails 无原生流式的缺陷，借鉴 Tauri Channel）。
/// </summary>
/// <remarks>
/// JS → C#（经 <c>invokeCSharpAction(body)</c>，body 为字符串）：
/// <list type="bullet">
/// <item><c>"C" + json</c>：RPC 调用。json = <see cref="CallMessage"/>。</item>
/// <item><c>"E" + json</c>：事件 emit。json = <see cref="EventMessage"/>。</item>
/// <item><c>"X" + channelId</c>：JS 关闭通道。</item>
/// </list>
/// C# → JS（经 <see cref="IRpcTransport.ExecuteScriptAsync"/>，执行 JS 表达式）：
/// <list type="bullet">
/// <item>resolve：<c>window.__lybox.resolve(callbackId, errJson, resultJson)</c></item>
/// <item>事件分发：<c>window.__lybox.dispatch(name, dataJson)</c></item>
/// <item>通道数据：<c>window.__lybox.channel.onData(channelId, dataJson)</c></item>
/// <item>通道关闭：<c>window.__lybox.channel.onClose(channelId)</c></item>
/// </list>
/// </remarks>
public static class RpcEnvelope
{
    public const char PrefixCall = 'C';
    public const char PrefixEvent = 'E';
    public const char PrefixChannelClose = 'X';

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // IPC 跨语言序列化：不转义非 ASCII（中文/emoji 等），让 JS 侧调试时可读。
        // 浏览器 JSON.parse 对原文与 \uXXXX 转义等价，但原文更省带宽、更易排查。
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };
}

/// <summary>JS→C# 调用消息。</summary>
public sealed record CallMessage
{
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("args")] public JsonElement[] Args { get; init; } = Array.Empty<JsonElement>();
    [JsonPropertyName("callbackId")] public string CallbackId { get; init; } = "";
}

/// <summary>JS→C# 事件消息。</summary>
public sealed record EventMessage
{
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("data")] public JsonElement? Data { get; init; }
}
