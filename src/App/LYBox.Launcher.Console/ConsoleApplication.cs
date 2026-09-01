using System.CommandLine;
using System.Reflection;
using LYBox.Layout.Core.Services;
using LYBox.Launcher.Desktop;
using LYBox.Plugin.Shared.CommandLine;
using LYBox.Plugin.Shared.Models;
using Spectre.Console;
using SpectreMarkup = Spectre.Console.Markup;

namespace LYBox.Launcher.Console;

/// <summary>
/// LYBox 控制台启动器：解析命令行参数并分发到内置 / 插件注册的命令。
/// 设计与 windit-toolbox <c>Avalonia.Launcher.Desktop.Console.ConsoleApplication</c> 一致：
/// 无参数时回退到 GUI 桌面启动以保持向后兼容；带参数时进入 CLI 模式。
/// </summary>
internal sealed class ConsoleApplication
{
    private readonly IAnsiConsole _console;
    private readonly Action<string[]> _startDesktop;
    private readonly string? _pluginsDirectory;

    /// <summary>
    /// 默认构造函数（生产代码使用）：桌面启动器为
    /// <see cref="LYBox.Launcher.Desktop.DesktopLauncher"/>，插件目录为默认基目录下的 <c>plugins/</c>。
    /// </summary>
    public ConsoleApplication(IAnsiConsole console)
        : this(console, startDesktop: null, pluginsDirectory: null)
    {
    }

    /// <summary>
    /// 注入式构造函数（测试使用）：可替换 <paramref name="startDesktop"/> 委托与
    /// <paramref name="pluginsDirectory"/> 路径，便于隔离运行。
    /// </summary>
    public ConsoleApplication(
        IAnsiConsole console,
        Action<string[]>? startDesktop,
        string? pluginsDirectory = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _startDesktop = startDesktop ?? DesktopLauncher.StartWithConsole;
        _pluginsDirectory = pluginsDirectory;
    }

    /// <summary>解析并执行 CLI 命令，返回进程退出码。</summary>
    public async Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
        {
            // 向后兼容：旧版 LYBox.Launcher.Console 直接走 GUI 模式。
            _startDesktop([]);
            return 0;
        }

        await using var pluginHost = await PluginCliHost.CreateAsync(_console, _pluginsDirectory)
            .ConfigureAwait(false);

        try
        {
            var rootCommand = CreateRootCommand(pluginHost);
            return rootCommand.Parse(args).Invoke();
        }
        catch (Exception exception)
        {
            _console.MarkupLine($"[red]CLI 执行失败：[/] {SpectreMarkup.Escape(exception.Message)}");
            return 1;
        }
    }

    private RootCommand CreateRootCommand(PluginCliHost pluginHost)
    {
        var root = new RootCommand("LYBox 桌面启动器与插件命令宿主")
        {
            TreatUnmatchedTokensAsErrors = true,
        };

        root.Subcommands.Add(CreateGuiCommand());
        root.Subcommands.Add(CreateVersionCommand());
        root.Subcommands.Add(CreatePluginsCommand());

        var pluginCommand = new Command("plugin", "执行已安装插件显式注册的 CLI 子命令。");
        pluginHost.RegisterCommands(pluginCommand);
        root.Subcommands.Add(pluginCommand);
        return root;
    }

    private Command CreateGuiCommand()
    {
        var arguments = new Argument<string[]>("arguments")
        {
            Description = "转发给桌面启动器的参数（例如 --no-splash、--collapsed-sidebar）。",
            Arity = ArgumentArity.ZeroOrMore
        };
        var command = new Command("gui", "启动 LYBox 桌面应用（与不带参数运行等价）。");
        command.Aliases.Add("desktop");
        command.Arguments.Add(arguments);
        command.SetAction(parseResult =>
        {
            _startDesktop(parseResult.GetValue(arguments) ?? []);
            return 0;
        });
        return command;
    }

    private Command CreateVersionCommand()
    {
        var command = new Command("version", "显示控制台启动器版本。");
        command.SetAction(_ =>
        {
            var version = typeof(ConsoleApplication).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
                ?? typeof(ConsoleApplication).Assembly.GetName().Version?.ToString()
                ?? "unknown";
            _console.MarkupLine($"LYBox Launcher [green]{SpectreMarkup.Escape(version)}[/]");
            return 0;
        });
        return command;
    }

    private Command CreatePluginsCommand()
    {
        var plugins = new Command("plugins", "查看已安装插件清单（不加载插件程序集）。");
        plugins.Subcommands.Add(CreatePluginsListCommand());
        plugins.Subcommands.Add(CreatePluginInfoCommand());
        return plugins;
    }

    private Command CreatePluginsListCommand()
    {
        var command = new Command("list", "列出已安装插件及其持久化状态。");
        command.SetAction(_ => ListPlugins());
        return command;
    }

    private Command CreatePluginInfoCommand()
    {
        var pluginId = new Argument<string>("plugin-id")
        {
            Description = "plugin.json 中的精确插件标识。"
        };
        var command = new Command("info", "展示一个已安装插件的清单详情。");
        command.Arguments.Add(pluginId);
        command.SetAction(parseResult => ShowPlugin(parseResult.GetValue(pluginId)!));
        return command;
    }

    private int ListPlugins()
    {
        using var loader = new PluginLoader(_pluginsDirectory);

        var installed = loader.GetInstalledPlugins()
            .OrderBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (installed.Length == 0)
        {
            _console.MarkupLine("[yellow]未发现任何已安装插件。[/]");
            return 0;
        }

        var table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("Plugin")
            .AddColumn("Id")
            .AddColumn("Version")
            .AddColumn("State");
        foreach (var plugin in installed)
        {
            table.AddRow(
                SpectreMarkup.Escape(plugin.Name),
                SpectreMarkup.Escape(plugin.PluginId),
                SpectreMarkup.Escape(plugin.Version),
                FormatState(plugin.State));
        }

        _console.Write(table);
        return 0;
    }

    private int ShowPlugin(string pluginId)
    {
        using var loader = new PluginLoader(_pluginsDirectory);

        var plugin = loader.GetPlugin(pluginId);
        if (plugin is null)
        {
            _console.MarkupLine($"[red]未找到插件：[/] {SpectreMarkup.Escape(pluginId)}");
            return 2;
        }

        var table = new Table().Border(TableBorder.Simple);
        table.AddColumn("Property");
        table.AddColumn("Value");
        table.AddRow("Name", SpectreMarkup.Escape(plugin.Name));
        table.AddRow("Id", SpectreMarkup.Escape(plugin.PluginId));
        table.AddRow("Version", SpectreMarkup.Escape(plugin.Version));
        table.AddRow("Author", SpectreMarkup.Escape(plugin.Author));
        table.AddRow("Description", SpectreMarkup.Escape(plugin.Description));
        table.AddRow("State", FormatState(plugin.State));
        table.AddRow("Assembly", SpectreMarkup.Escape(plugin.AssemblyPath ?? string.Empty));
        table.AddRow("Install path", SpectreMarkup.Escape(plugin.InstallPath ?? string.Empty));
        _console.Write(table);
        return 0;
    }

    private static string FormatState(PluginState state)
    {
        var color = state switch
        {
            PluginState.Loaded => "green",
            PluginState.Installed => "green",
            PluginState.Error => "red",
            PluginState.Disabled or PluginState.PendingUninstall or PluginState.PendingUpgrade => "yellow",
            _ => "grey"
        };
        return $"[{color}]{SpectreMarkup.Escape(state.ToString())}[/]";
    }
}