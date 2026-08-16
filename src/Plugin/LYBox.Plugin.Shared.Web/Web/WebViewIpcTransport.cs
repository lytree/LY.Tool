using Avalonia.Controls;
using LYBox.Plugin.Shared.Rpc;

namespace LYBox.Plugin.Shared.Web;

/// <summary>
/// IRpcTransport 实现：桥接 Avalonia.Controls.WebView 的原生 IPC 原语。
/// </summary>
/// <remarks>
/// <para>
/// 对应 Avalonia.Controls.WebView 的两个原生通道（跨平台一致）：
/// <list type="bullet">
/// <item>JS→C#：<c>WebView.WebMessageReceived</c> 事件（JS 经 <c>invokeCSharpAction(body)</c> 发来字符串）。</item>
/// <item>C#→JS：<c>WebView.InvokeScript(js)</c> 执行任意 JS 表达式，返回 <c>Task&lt;string?&gt;</c>。</item>
/// </list>
/// </para>
/// <para>
/// 通道为纯字符串、fire-and-forget（JS→C# 侧），与 Wails v2 传输模型一致。
/// 调用方应在控件卸载时调用 <see cref="Detach"/> 解绑事件，避免泄漏。
/// </para>
/// </remarks>
public sealed class WebViewIpcTransport : IRpcTransport
{
    private readonly NativeWebView _webView;

    public WebViewIpcTransport(NativeWebView webView)
    {
        _webView = webView;
        _webView.WebMessageReceived += OnMessageReceived;
    }

    /// <inheritdoc />
    public event Action<string?>? MessageReceived;

    /// <inheritdoc />
    public async Task<string?> ExecuteScriptAsync(string javaScript, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Avalonia.Controls.WebView.InvokeScript 返回 Task<string?>（任意 JS 表达式求值结果）
        return await _webView.InvokeScript(javaScript).ConfigureAwait(false);
    }

    private void OnMessageReceived(object? sender, WebMessageReceivedEventArgs e)
    {
        // e.Body 是 JS 经 invokeCSharpAction 发来的字符串（可能为 null）
        MessageReceived?.Invoke(e.Body);
    }

    /// <summary>解绑 WebMessageReceived 事件。应在控件卸载时调用，避免事件泄漏。</summary>
    public void Detach()
    {
        _webView.WebMessageReceived -= OnMessageReceived;
    }
}
