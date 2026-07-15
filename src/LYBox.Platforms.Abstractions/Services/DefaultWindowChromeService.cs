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
    public bool NeedsSelfDrawnTitleBar => true;

    /// <inheritdoc />
    public void ApplyChrome(Window window)
    {
        if (window is null) return;

        if (OperatingSystem.IsLinux())
        {
            // Linux/X11: Avalonia 12.1 的 X11Window.UpdateMotifHints 中，WindowDecorations.BorderOnly
            // 不会移除 WM 标题栏（仅 WindowDecorations.None 才设置 decorations=0），BorderOnly 在 X11 上
            // 等同于 Full——原生标题栏仍然显示。
            // 解决方案：启用 ExtendClientAreaToDecorationsHint 将客户区扩展覆盖原生标题栏。
            // Avalonia 12 中 WindowDecorations.BorderOnly 已整合了旧版 ExtendClientAreaChromeHints.NoChrome
            // 的语义——UrsaWindow 的 WindowDrawnDecorations.HasTitleBar=false，不渲染 overlay 标题栏/caption 按钮。
            // WM 原生 resize 边框在窗口边缘，仍可正常工作。
            window.ExtendClientAreaToDecorationsHint = true;
            window.ExtendClientAreaTitleBarHeightHint = -1;
            window.WindowDecorations = WindowDecorations.BorderOnly;
        }
        else
        {
            // Windows/macOS: WindowDecorations.BorderOnly 正确移除原生标题栏并保留 OS resize frame。
            // 关闭 ExtendClientAreaToDecorationsHint：避免客户区覆盖 resize frame 导致缩放失效
            // （Avalonia 12 中 BorderOnly + ExtendClientArea=true 会使原生 resize 失效）。
            window.ExtendClientAreaToDecorationsHint = false;
            window.WindowDecorations = WindowDecorations.BorderOnly;
        }
    }
}
