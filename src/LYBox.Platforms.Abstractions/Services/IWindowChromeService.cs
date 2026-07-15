using Avalonia.Controls;

namespace LYBox.Platforms.Abstraction.Services;

/// <summary>
/// 窗口自定义边框（chrome）服务接口。
/// </summary>
/// <remarks>
/// <para>
/// Avalonia 的 <c>ExtendClientAreaToDecorationsHint</c> 在 Windows 与 macOS 上能将客户区扩展到标题栏区域，
/// 从而让 <c>WindowDrawnDecorations</c> 绘制自定义标题栏与标题按钮。但在 Linux 上，该机制依赖
/// <c>_GTK_FRAME_EXTENTS</c>，仅在部分 GTK 兼容窗口管理器（如 Mutter/GNOME）上生效；在 KWin、Sway 等
/// WM/合成器上要么出现原生标题栏与自绘标题栏重叠，要么客户区无法扩展，导致自定义边框无法展示。
/// </para>
/// <para>
/// 本服务按平台选择合适的 chrome 策略：Windows/macOS 走原生扩展客户区；Linux 回退到
/// <see cref="Avalonia.Controls.WindowDecorations.BorderOnly"/>（移除原生标题栏但保留 WM 的缩放边框与阴影），
/// 并由应用层自绘 <c>FluentTitleBar</c> 承担标题栏职责。
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
    /// <value>当平台不支持扩展客户区（Linux）时为 <c>true</c>。</value>
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
