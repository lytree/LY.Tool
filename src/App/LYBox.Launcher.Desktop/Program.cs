using Avalonia;
using Avalonia.Dialogs;
using System;
using System.Linq;

namespace LYBox.Launcher.Desktop;

/// <summary>
/// 桌面启动入口。类型为 public 以允许控制台调试版（LYBox.Launcher.Console）复用
/// <see cref="BuildAvaloniaApp"/> 启动同一套应用。
/// </summary>
public sealed class Program
{
    public static string[]? LaunchArgs { get; private set; }

    public static bool NoSplash => HasArg("--no-splash");
    public static bool CollapsedSidebar => HasArg("--collapsed-sidebar");

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        LaunchArgs = args;
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
                .UseManagedSystemDialogs()
                .UsePlatformDetect()
                .With(new Win32PlatformOptions())
                .LogToTrace();
    }

    public static bool HasArg(string name) =>
        LaunchArgs?.Contains(name, StringComparer.OrdinalIgnoreCase) == true;

    public static string? GetArgValue(string prefix)
    {
        if (LaunchArgs == null) return null;
        foreach (var arg in LaunchArgs)
        {
            if (arg.StartsWith(prefix + "=", StringComparison.OrdinalIgnoreCase))
                return arg.Substring(prefix.Length + 1);
        }
        return null;
    }
}
