using Avalonia.Controls;

namespace LYBox.Platforms.Abstraction.Services;

/// <summary>
/// <see cref="IWindowChromeService"/> 的默认实现。
/// </summary>
/// <remarks>
/// 通过 <see cref="System.OperatingSystem"/> 在运行时判断平台，无需为每个平台单独编译程序集。
/// 与仓库中 <c>PluginLoadContext</c> 等处使用的 <c>OperatingSystem.Is*()</c> 模式一致。
/// </remarks>
public class DefaultWindowChromeService : IWindowChromeService
{
    /// <inheritdoc />
    public bool SupportsExtendClientArea => !OperatingSystem.IsLinux();

    /// <inheritdoc />
    public bool NeedsSelfDrawnTitleBar => OperatingSystem.IsLinux();

    /// <inheritdoc />
    public void ApplyChrome(Window window)
    {
        if (window is null) return;

        if (OperatingSystem.IsLinux())
        {
            // Linux：Avalonia 的 ExtendClientAreaToDecorationsHint 依赖 _GTK_FRAME_EXTENTS，
            // 在非 GTK WM（KWin/Sway 等）上无法可靠扩展客户区，会出现原生标题栏与自绘标题栏重叠
            // 或自定义边框不展示。回退方案：
            //   - WindowDecorations.BorderOnly：移除原生标题栏，但保留 WM 的缩放边框与阴影；
            //   - 关闭 ExtendClientAreaToDecorationsHint，避免触发 Avalonia 的 WindowDrawnDecorations 双重绘制；
            //   - 标题栏职责交由应用层自绘的 FluentTitleBar 承担（拖动走 BeginMoveDrag）。
            window.ExtendClientAreaToDecorationsHint = false;
            window.WindowDecorations = WindowDecorations.BorderOnly;
        }
        else
        {
            // Windows / macOS：原生扩展客户区可用，由 Avalonia 的 WindowDrawnDecorations 绘制标题栏与标题按钮。
            window.ExtendClientAreaToDecorationsHint = true;
            window.ExtendClientAreaTitleBarHeightHint = -1;
            window.WindowDecorations = WindowDecorations.Full;
        }
    }
}
