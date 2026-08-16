namespace LYBox.Plugin.Shared.Rpc;

/// <summary>
/// 底层双向传输抽象。把 IPC 运行时与具体 WebView 控件解耦——
/// 任何能"接收 JS 字符串 + 执行 JS"的载体都可实现此接口。
/// </summary>
/// <remarks>
/// 对应 Avalonia.Controls.WebView 的两个原语：
/// <list type="bullet">
/// <item><see cref="MessageReceived"/> ← <c>WebMessageReceived</c> 事件（JS 经 <c>invokeCSharpAction(body)</c> 发来）。</item>
/// <item><see cref="ExecuteScriptAsync"/> ← <c>webView.InvokeScript(js)</c>。</item>
/// </list>
/// 通道为纯字符串、fire-and-forget（JS→C# 侧），与 Wails v2 传输模型一致。
/// </remarks>
public interface IRpcTransport
{
    /// <summary>JS 经 <c>invokeCSharpAction</c> 发来的字符串消息（可能为 null）。</summary>
    event Action<string?>? MessageReceived;

    /// <summary>在页面上下文执行任意 JS 表达式，返回其求值结果（JSON 字符串）。</summary>
    Task<string?> ExecuteScriptAsync(string javaScript, CancellationToken cancellationToken = default);
}
