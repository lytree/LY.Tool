using Avalonia;

namespace LYBox.Launcher.Console;

/// <summary>
/// 控制台启动入口：与 GUI 版（LYBox.Launcher.Desktop）共用同一套 Avalonia 应用与 DI 启动逻辑，
/// 但以控制台子系统（OutputType=Exe）运行，便于直接查看 ZLogger 日志与异常堆栈。
/// 发布产物：LYBox.Launcher.Console.exe
/// </summary>
sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 复用 GUI 版 Program 的 AppBuilder（Configure<App> + UsePlatformDetect + LogToTrace）
        LYBox.Launcher.Desktop.Program.BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }
}
