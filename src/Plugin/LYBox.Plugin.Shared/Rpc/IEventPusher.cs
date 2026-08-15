namespace LYBox.Plugin.Shared.Rpc;

/// <summary>
/// C# → JS 主动推送抽象。用于在不依赖 <c>InvokeScript</c> 的前提下，
/// 通过 SSE（Server-Sent Events）等长连接通道把事件 / 通道数据推送给前端。
/// </summary>
/// <remarks>
/// <para>
/// 设计动机：Avalonia.Controls.WebView 的 <c>InvokeScript</c> 适合 RPC 同步回推（resolve），
/// 但用于高频推送（事件分发 / 流式通道数据）时会受 WebView 单帧 JS 队列限制，堆积易丢消息。
/// SSE 提供独立的 HTTP 长连接通道，由浏览器 <c>EventSource</c> 原生支持自动重连，
/// 与 <c>InvokeScript</c> 解耦后两者各司其职：
/// <list type="bullet">
/// <item><c>InvokeScript</c> → <c>window.__lybox.resolve</c>（RPC 结果回推，必须同步返回给 Promise）</item>
/// <item>SSE → <c>dispatch</c> / <c>channel-data</c> / <c>channel-close</c>（异步推送）</item>
/// </list>
/// </para>
/// <para>
/// 当 <see cref="WebViewIpcHost"/> 注入了非 null 的 <c>IEventPusher</c> 时，
/// <c>EmitEventAsync</c> / <c>Channel.WriteAsync</c> 优先走 SSE；否则降级到 <c>InvokeScript</c>。
/// </para>
/// </remarks>
public interface IEventPusher
{
    /// <summary>
    /// 向指定插件的所有 SSE 订阅客户端推送一条消息。
    /// </summary>
    /// <param name="pluginId">目标插件 ID（与 SSE 路由 <c>/sse/{pluginId}</c> 一致）。</param>
    /// <param name="eventType">事件类型：<c>dispatch</c> / <c>channel-data</c> / <c>channel-close</c> / <c>ready</c>。</param>
    /// <param name="json">消息体 JSON 字符串（不带外层包装，由实现负责拼接 <c>event:</c> / <c>data:</c> 行）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task PushAsync(string pluginId, string eventType, string json, CancellationToken cancellationToken = default);
}
