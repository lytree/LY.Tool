using Avalonia;
using Avalonia.Dialogs;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace LYBox.Launcher.Desktop;

/// <summary>
/// 桌面启动入口。类型为 public 以允许控制台调试版（LYBox.Launcher.Console）复用
/// <see cref="BuildAvaloniaApp"/> 启动同一套应用。
/// </summary>
public sealed partial class Program
{
    private const string ConsoleModeArgument = "--console";

    public static string[]? LaunchArgs { get; private set; }

    public static bool NoSplash => HasArg("--no-splash");
    public static bool CollapsedSidebar => HasArg("--collapsed-sidebar");

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var consoleMode = args.Any(argument =>
            string.Equals(argument, ConsoleModeArgument, StringComparison.OrdinalIgnoreCase));
        var applicationArgs = consoleMode
            ? args.Where(argument => !string.Equals(argument, ConsoleModeArgument, StringComparison.OrdinalIgnoreCase)).ToArray()
            : args;

        LaunchArgs = applicationArgs;

        if (consoleMode)
        {
            StartWithConsole(applicationArgs);
            return;
        }

        StartDesktop(applicationArgs);
    }

    internal static void StartWithConsole(string[] args)
    {
        var ownsConsole = TryCreateConsole();

        try
        {
            StartDesktop(args);
        }
        finally
        {
            if (ownsConsole)
            {
                FreeConsole();
            }
        }
    }

    private static void StartDesktop(string[] args)
    {
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static bool TryCreateConsole()
    {
        if (!OperatingSystem.IsWindows() || !AllocConsole())
        {
            // The process may already be attached to the invoking terminal.
            return false;
        }

        Console.OutputEncoding = Encoding.UTF8;
        var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
        Console.SetOut(output);
        Console.SetError(output);
        return true;
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

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllocConsole();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FreeConsole();
}

/// <summary>
/// 供控制台调试版（LYBox.Launcher.Console）调用的启动入口。
/// </summary>
public static class DesktopLauncher
{
    public static void StartWithConsole(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        Program.StartWithConsole(args);
    }
}