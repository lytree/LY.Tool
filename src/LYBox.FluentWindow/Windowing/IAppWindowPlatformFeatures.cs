using Avalonia.Media;

namespace LYBox.FluentWindow.Windowing;

/// <summary>
/// Represents the visual state of the operating-system taskbar progress overlay
/// (Windows-specific; stubbed on other platforms).
/// </summary>
public enum TaskBarProgressBarState
{
    /// <summary>No progress indicator is shown.</summary>
    None = 0,

    /// <summary>Normal (green) progress indicator.</summary>
    Normal = 1,

    /// <summary>Paused (yellow) progress indicator.</summary>
    Paused = 2,

    /// <summary>Error (red) progress indicator.</summary>
    Error = 3,

    /// <summary>Indeterminate (marquee) progress indicator.</summary>
    Indeterminate = 4
}

/// <summary>
/// Provides access to platform-specific window chrome features such as custom
/// window border color and the taskbar progress overlay.
/// </summary>
/// <remarks>
/// This is a simplified port of the AvaloniaFluentUI <c>IAppWindowPlatformFeatures</c>
/// contract. The original Win32 interop dependencies have been removed; implementations
/// are stubbed (see <see cref="Win32.Win32FluentWindowFeatures"/>).
/// </remarks>
public interface IAppWindowPlatformFeatures
{
    /// <summary>Sets the accent color of the native window border.</summary>
    void SetWindowBorderColor(Color color);

    /// <summary>Sets the visual state of the taskbar progress overlay.</summary>
    void SetTaskBarProgressBarState(TaskBarProgressBarState state);

    /// <summary>Sets the value of the taskbar progress overlay.</summary>
    /// <param name="value">The current progress value.</param>
    /// <param name="maximum">The maximum progress value.</param>
    void SetTaskBarProgressBarValue(ulong value, ulong maximum);
}
