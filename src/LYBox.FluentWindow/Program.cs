using Avalonia;
using System;
using System.Linq;

namespace LYBox.FluentWindow;

class Program
{
    public static string[]? LaunchArgs { get; private set; }

    [STAThread]
    static void Main(string[] args)
    {
        LaunchArgs = args;
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    public static bool HasArg(string name) =>
        LaunchArgs?.Contains(name, StringComparer.OrdinalIgnoreCase) == true;
}
