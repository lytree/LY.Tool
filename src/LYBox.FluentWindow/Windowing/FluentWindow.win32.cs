using System;

namespace LYBox.FluentWindow.Windowing;

/// <summary>
/// Win32 platform initialization for <see cref="FluentWindow"/>.
/// </summary>
/// <remarks>
/// Simplified port of the AvaloniaFluentUI Win32 partial. The upstream dependency on
/// <c>AvaloniaFluentUI.Controls.Interop.Win32Interop</c> has been removed — the Win32
/// features object is the stubbed <see cref="Win32.Win32FluentWindowFeatures"/>.
/// </remarks>
public partial class FluentWindow
{
    partial void InitializeWindowPlatform()
    {
        IsWindows = true;
        IsWindows11 = IsRunningOnWindows11();

        // Win32 platform features are stubbed in this port — see Win32FluentWindowFeatures.
        PlatformFeatures = new Win32FluentWindowFeatures(this);
    }

    /// <summary>
    /// Returns true if the current process is running on Windows 11 (build 22000+).
    /// Uses <see cref="OperatingSystem.IsWindowsVersionAtLeast(int, int, int)"/>; falls
    /// back to <c>false</c> on non-Windows platforms or when the version cannot be read.
    /// </summary>
    private static bool IsRunningOnWindows11()
    {
        try
        {
            // Windows 11 starts at build 22000. The 10.0 major/minor are kept for parity
            // with the underlying Windows NT version reported by the OS.
            return OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);
        }
        catch
        {
            return false;
        }
    }
}
