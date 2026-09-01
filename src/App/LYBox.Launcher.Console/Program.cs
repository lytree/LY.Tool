using Spectre.Console;

namespace LYBox.Launcher.Console;

/// <summary>
/// 控制台启动入口（LYBox.Launcher.Console.exe）。
/// <para>
/// 与 GUI 版（<see cref="LYBox.Launcher.Desktop"/>.exe）共用同一套 Avalonia 应用与 DI 启动逻辑，
/// 同时提供命令行接口（<c>System.CommandLine</c> + <c>Spectre.Console</c>）：
/// </para>
/// <list type="bullet">
/// <item>无参数 / <c>gui</c> → 启动 Avalonia 桌面应用（保持向后兼容）</item>
/// <item><c>version</c> → 打印控制台启动器版本</item>
/// <item><c>plugins list|info &lt;id&gt;</c> → 检查已安装插件清单（不加载程序集）</item>
/// <item><c>plugin &lt;name&gt; ...</c> → 加载已安装插件并执行由 <c>IPluginCommandRegistrar</c> 注册的子命令</item>
/// </list>
/// <para>
/// 详细帮助：<c>LYBox.Launcher.Console.exe --help</c>
/// </para>
/// </summary>
internal static class Program
{
    [STAThread]
    private static Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return new ConsoleApplication(AnsiConsole.Console).RunAsync(args);
    }
}
