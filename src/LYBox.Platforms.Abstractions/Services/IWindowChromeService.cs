using Avalonia.Controls;

namespace LYBox.Platforms.Abstraction.Services;

/// <summary>
/// 窗口自定义边框（chrome）服务接口。
/// </summary>
/// <remarks>
/// <para>
/// 按平台选择不同的 chrome 策略，在保留原生 resize 能力的同时实现统一的 Fluent 自绘标题栏。
/// 标题栏职责（Title、caption 按钮、拖动）由应用层 MainView 工具栏自绘承担。
/// </para>
/// <para>
/// - Windows/macOS：<see cref="Avalonia.Controls.WindowDecorations.BorderOnly"/> + <c>ExtendClientAreaToDecorationsHint=false</c>。
///   BorderOnly 正确移除原生标题栏并保留 OS resize frame。关闭 ExtendClientArea 避免覆盖 resize frame 导致缩放失效。
/// </para>
/// <para>
/// - Linux/X11：<see cref="Avalonia.Controls.WindowDecorations.BorderOnly"/> + <c>ExtendClientAreaToDecorationsHint=true</c>。
///   Avalonia 12.1 的 X11 实现中 BorderOnly 不会移除 WM 标题栏（bug），需启用 ExtendClientArea 将客户区扩展覆盖原生标题栏。
///   Avalonia 12 中 BorderOnly 已整合旧版 NoChrome 语义，不渲染 overlay caption 按钮。WM 原生 resize 边框在窗口边缘仍可正常工作。
/// </para>
/// </remarks>
public interface IWindowChromeService
{
    /// <summary>
    /// 当前平台是否支持将客户区扩展到标题栏区域。
    /// </summary>
    /// <value>Windows 与 macOS 为 <c>true</c>；Linux 为 <c>false</c>，需由应用自绘标题栏。</value>
    bool SupportsExtendClientArea { get; }

    /// <summary>
    /// 是否需要应用层自绘标题栏。
    /// </summary>
    /// <value>当前实现下所有平台均为 <c>true</c>（统一由工具栏自绘 caption 按钮与 Title）。</value>
    bool NeedsSelfDrawnTitleBar { get; }

    /// <summary>
    /// 将平台对应的 chrome 策略应用到指定窗口。
    /// </summary>
    /// <param name="window">要应用 chrome 的窗口。</param>
    /// <remarks>
    /// 应在窗口构造完成、显示之前调用（通常在 <c>InitializeComponent()</c> 之后）。
    /// </remarks>
    void ApplyChrome(Window window);
}
