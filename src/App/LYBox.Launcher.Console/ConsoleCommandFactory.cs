using System.CommandLine;
using System.CommandLine.Parsing;
using System.Reflection;
using LYBox.Layout.Core.Services;
using LYBox.Plugin.Shared.CommandLine;
using LYBox.Plugin.Shared.Models;
using LYBox.Plugin.Shared.Services;
using Spectre.Console;
using SpectreMarkup = Spectre.Console.Markup;

namespace LYBox.Launcher.Console;

internal sealed class ConsoleCommandFactory
{
    private readonly IAnsiConsole _console;
    private readonly Action<string[]> _startDesktop;
    private readonly Func<CancellationToken, Task<IPluginManagementService>> _getPluginManagementService;
    private readonly Func<CliOutputFormat, CliOutput> _createOutput;

    public ConsoleCommandFactory(
        IAnsiConsole console,
        Action<string[]> startDesktop,
        Func<CancellationToken, Task<IPluginManagementService>> getPluginManagementService,
        Func<CliOutputFormat, CliOutput> createOutput)
    {
        _console = console;
        _startDesktop = startDesktop;
        _getPluginManagementService = getPluginManagementService;
        _createOutput = createOutput;
    }

    public ConsoleCommandTree CreateBootstrapCommandTree() => CreateCommandTree(null, null);

    public ConsoleCommandTree CreatePluginCommandTree(
        IPluginCliHost pluginHost,
        PluginBootstrapInvocation invocation) => CreateCommandTree(pluginHost, invocation);

    private ConsoleCommandTree CreateCommandTree(
        IPluginCliHost? pluginHost,
        PluginBootstrapInvocation? invocation)
    {
        var output = CreateOutputOption();
        var root = new RootCommand("LYBox desktop launcher and plugin command host");
        root.Options.Add(output);
        root.SetAction(parseResult => StartDesktop(
            parseResult.GetValue(output),
            []));
        root.Subcommands.Add(CreateGuiCommand(output));
        root.Subcommands.Add(CreateVersionCommand(output));
        var plugins = CreatePluginsCommand(output);
        root.Subcommands.Add(plugins);

        var plugin = new Command("plugin", "Run a CLI command provided by an installed plugin.");
        var run = new Command("run", "Load one plugin by id and run its CLI command.");
        plugin.Subcommands.Add(run);
        root.Subcommands.Add(plugin);

        Argument<string?>? pluginAlias = null;
        Argument<string?>? pluginId = null;
        var registeredPluginCommands = 0;
        if (pluginHost is null || invocation is null)
        {
            pluginAlias = AddBootstrapArguments(plugin, "plugin-alias", "Alias from plugin.cli.json.");
            pluginId = AddBootstrapArguments(run, "plugin-id", "Exact plugin id from plugin.json.");
            plugin.SetAction(_ => PluginCliExitCodes.Success);
            run.SetAction(_ => PluginCliExitCodes.Success);
        }
        else
        {
            if (invocation.Route == PluginInvocationRoute.Explicit)
            {
                pluginId = new Argument<string?>("plugin-id")
                {
                    Description = "Exact plugin id from plugin.json."
                };
                pluginId.Validators.Add(result =>
                {
                    if (!string.Equals(
                            result.GetValueOrDefault<string>(),
                            invocation.Target,
                            StringComparison.Ordinal))
                    {
                        result.AddError($"Expected plugin id '{invocation.Target}'.");
                    }
                });
                run.Arguments.Add(pluginId);
            }

            registeredPluginCommands = pluginHost.RegisterCommands(plugin, run, invocation.Route);
        }

        return new ConsoleCommandTree(
            root,
            plugins,
            plugin,
            run,
            output,
            pluginAlias,
            pluginId,
            registeredPluginCommands);
    }

    private static Argument<string?> AddBootstrapArguments(
        Command command,
        string name,
        string description)
    {
        var target = new Argument<string?>(name)
        {
            Description = description,
            Arity = ArgumentArity.ZeroOrOne
        };
        var remaining = new Argument<string[]>("plugin-arguments")
        {
            Description = "Arguments parsed by the hydrated plugin command tree.",
            Arity = ArgumentArity.ZeroOrMore
        };
        command.Arguments.Add(target);
        command.Arguments.Add(remaining);
        command.TreatUnmatchedTokensAsErrors = false;
        return target;
    }

    private Command CreateGuiCommand(Option<CliOutputFormat> output)
    {
        var arguments = new Argument<string[]>("arguments")
        {
            Description = "Arguments forwarded to the desktop launcher.",
            Arity = ArgumentArity.ZeroOrMore
        };
        var command = new Command("gui", "Start the LYBox desktop application.");
        command.Aliases.Add("desktop");
        command.Arguments.Add(arguments);
        command.SetAction(parseResult => StartDesktop(
            parseResult.GetValue(output),
            parseResult.GetValue(arguments) ?? []));
        return command;
    }

    private Command CreateVersionCommand(Option<CliOutputFormat> outputOption)
    {
        var command = new Command("version", "Display the launcher version.");
        command.SetAction(parseResult =>
        {
            var version = typeof(ConsoleApplication).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
                ?? typeof(ConsoleApplication).Assembly.GetName().Version?.ToString()
                ?? "unknown";
            var output = _createOutput(parseResult.GetValue(outputOption));
            if (output.Format == CliOutputFormat.Json)
                output.WriteSuccess("version", new { version });
            else
                _console.MarkupLine($"LYBox Launcher [green]{SpectreMarkup.Escape(version)}[/]");
            return PluginCliExitCodes.Success;
        });
        return command;
    }

    private Command CreatePluginsCommand(Option<CliOutputFormat> output)
    {
        var plugins = new Command(
            "plugins",
            "List plugin details, install ZIP packages, or schedule plugin removal.");
        plugins.Subcommands.Add(CreatePluginsListCommand(output));
        plugins.Subcommands.Add(CreatePluginInfoCommand(output));
        plugins.Subcommands.Add(CreatePluginInstallCommand(output));
        plugins.Subcommands.Add(CreatePluginUninstallCommand(output));
        return plugins;
    }

    private Command CreatePluginsListCommand(Option<CliOutputFormat> output)
    {
        var command = new Command("list", "List installed plugins.");
        command.SetAction((parseResult, cancellationToken) =>
            ListPluginsAsync(parseResult.GetValue(output), cancellationToken));
        return command;
    }

    private Command CreatePluginInfoCommand(Option<CliOutputFormat> output)
    {
        var pluginId = new Argument<string>("plugin-id")
        {
            Description = "Exact plugin id from plugin.json."
        };
        var command = new Command("info", "Display one installed plugin.");
        command.Arguments.Add(pluginId);
        command.SetAction((parseResult, cancellationToken) => ShowPluginAsync(
            parseResult.GetRequiredValue(pluginId),
            parseResult.GetValue(output),
            cancellationToken));
        return command;
    }

    private Command CreatePluginInstallCommand(Option<CliOutputFormat> output)
    {
        var packagePath = new Argument<string>("package-path")
        {
            Description = "Path to a .zip plugin package."
        };
        var command = new Command("install", "Install or stage an upgrade from a plugin ZIP package.");
        command.Aliases.Add("add");
        command.Arguments.Add(packagePath);
        command.SetAction((parseResult, cancellationToken) => InstallPluginAsync(
            parseResult.GetRequiredValue(packagePath),
            parseResult.GetValue(output),
            cancellationToken));
        return command;
    }

    private Command CreatePluginUninstallCommand(Option<CliOutputFormat> output)
    {
        var pluginId = new Argument<string>("plugin-id")
        {
            Description = "Exact plugin id shown by 'plugins list'."
        };
        var command = new Command("uninstall", "Schedule a managed plugin for removal after restart.");
        command.Aliases.Add("remove");
        command.Arguments.Add(pluginId);
        command.SetAction((parseResult, cancellationToken) => UninstallPluginAsync(
            parseResult.GetRequiredValue(pluginId),
            parseResult.GetValue(output),
            cancellationToken));
        return command;
    }

    private async Task<int> ListPluginsAsync(
        CliOutputFormat outputFormat,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var plugins = (await _getPluginManagementService(cancellationToken).ConfigureAwait(false))
            .GetInstalledPlugins();
        var output = _createOutput(outputFormat);
        if (outputFormat == CliOutputFormat.Json)
        {
            output.WriteSuccess("plugins.list", new
            {
                plugins = plugins.Select(ToOutputModel).ToArray(),
                diagnostics = Array.Empty<string>()
            });
            return PluginCliExitCodes.Success;
        }

        if (plugins.Count == 0)
        {
            _console.MarkupLine("[yellow]No installed plugins were found.[/]");
            return PluginCliExitCodes.Success;
        }

        var table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("Plugin")
            .AddColumn("Id")
            .AddColumn("Version")
            .AddColumn("State");
        foreach (var plugin in plugins)
        {
            table.AddRow(
                SpectreMarkup.Escape(plugin.Name),
                SpectreMarkup.Escape(plugin.PluginId),
                SpectreMarkup.Escape(plugin.Version),
                FormatState(plugin.State));
        }

        _console.Write(table);
        return PluginCliExitCodes.Success;
    }

    private async Task<int> ShowPluginAsync(
        string pluginId,
        CliOutputFormat outputFormat,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var plugin = (await _getPluginManagementService(cancellationToken).ConfigureAwait(false))
            .GetPlugin(pluginId);
        if (plugin is null)
        {
            throw new CliFailureException(
                PluginCliExitCodes.NotFound,
                "plugin_not_found",
                $"Plugin '{pluginId}' was not found.",
                new { pluginId });
        }

        var output = _createOutput(outputFormat);
        if (outputFormat == CliOutputFormat.Json)
        {
            output.WriteSuccess("plugins.info", ToOutputModel(plugin));
            return PluginCliExitCodes.Success;
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
        table.AddRow("Assembly", SpectreMarkup.Escape(plugin.AssemblyPath));
        table.AddRow("Install path", SpectreMarkup.Escape(plugin.InstallPath));
        _console.Write(table);
        return PluginCliExitCodes.Success;
    }

    private async Task<int> InstallPluginAsync(
        string packagePath,
        CliOutputFormat outputFormat,
        CancellationToken cancellationToken)
    {
        var result = await (await _getPluginManagementService(cancellationToken).ConfigureAwait(false))
            .InstallFromFileAsync(packagePath, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success || result.PluginInfo is null)
        {
            throw new CliFailureException(
                ToExitCode(result.ErrorCode),
                ToInstallErrorCode(result.ErrorCode),
                result.ErrorMessage ?? "Plugin installation failed.");
        }

        var output = _createOutput(outputFormat);
        if (outputFormat == CliOutputFormat.Json)
        {
            output.WriteSuccess("plugins.install", new
            {
                plugin = ToOutputModel(result.PluginInfo),
                restartRequired = true
            });
        }
        else
        {
            var action = result.PluginInfo.State == PluginState.PendingUpgrade
                ? "Upgrade scheduled"
                : "Installed";
            _console.MarkupLine(
                $"[green]{action}[/] {SpectreMarkup.Escape(result.PluginInfo.Name)} " +
                $"([grey]{SpectreMarkup.Escape(result.PluginInfo.PluginId)}[/]) " +
                SpectreMarkup.Escape(result.PluginInfo.Version));
            _console.MarkupLine("[yellow]Restart the desktop application to activate the change.[/]");
        }

        return PluginCliExitCodes.Success;
    }

    private async Task<int> UninstallPluginAsync(
        string pluginId,
        CliOutputFormat outputFormat,
        CancellationToken cancellationToken)
    {
        var result = await (await _getPluginManagementService(cancellationToken).ConfigureAwait(false))
            .UninstallAsync(pluginId, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success || result.PluginInfo is null)
        {
            throw new CliFailureException(
                ToExitCode(result.ErrorCode),
                result.ErrorCode == PluginManagementErrorCode.NotFound
                    ? "plugin_not_found"
                    : "plugin_conflict",
                result.ErrorMessage ?? $"Failed to uninstall plugin '{pluginId}'.");
        }

        var output = _createOutput(outputFormat);
        if (outputFormat == CliOutputFormat.Json)
        {
            output.WriteSuccess("plugins.uninstall", new
            {
                plugin = ToOutputModel(result.PluginInfo),
                alreadyPending = result.AlreadyPending,
                restartRequired = true
            });
        }
        else
        {
            var action = result.AlreadyPending ? "Already pending uninstall" : "Uninstall scheduled";
            var color = result.AlreadyPending ? "yellow" : "green";
            _console.MarkupLine(
                $"[{color}]{action}[/] {SpectreMarkup.Escape(result.PluginInfo.Name)} " +
                $"({SpectreMarkup.Escape(result.PluginInfo.PluginId)}).");
            _console.MarkupLine("[yellow]Restart the desktop application to complete removal.[/]");
        }

        return PluginCliExitCodes.Success;
    }

    private static Option<CliOutputFormat> CreateOutputOption() =>
        new("--output", "-o")
        {
            Description = "Output format: text or json.",
            DefaultValueFactory = _ => CliOutputFormat.Text,
            Recursive = true
        };

    private int StartDesktop(CliOutputFormat outputFormat, string[] arguments)
    {
        if (outputFormat == CliOutputFormat.Json)
        {
            throw new CliFailureException(
                PluginCliExitCodes.Unsupported,
                "output_not_supported",
                "GUI launch does not support JSON output.");
        }

        _startDesktop(arguments);
        return PluginCliExitCodes.Success;
    }

    private static int ToExitCode(PluginManagementErrorCode errorCode) => errorCode switch
    {
        PluginManagementErrorCode.NotFound => PluginCliExitCodes.NotFound,
        PluginManagementErrorCode.Conflict => PluginCliExitCodes.Conflict,
        PluginManagementErrorCode.PermissionDenied => PluginCliExitCodes.Security,
        PluginManagementErrorCode.HostError => PluginCliExitCodes.HostFailure,
        _ => PluginCliExitCodes.ValidationFailed
    };

    private static string ToInstallErrorCode(PluginManagementErrorCode errorCode) => errorCode switch
    {
        PluginManagementErrorCode.NotFound => "package_not_found",
        PluginManagementErrorCode.Conflict => "plugin_conflict",
        PluginManagementErrorCode.PermissionDenied => "permission_denied",
        PluginManagementErrorCode.HostError => "installation_failed",
        _ => "invalid_package"
    };

    private static object ToOutputModel(PluginInfo plugin) => new
    {
        id = plugin.PluginId,
        plugin.Name,
        plugin.Version,
        plugin.Author,
        plugin.Description,
        state = plugin.State.ToString(),
        plugin.Dependencies,
        assembly = plugin.AssemblyPath,
        installPath = plugin.InstallPath,
        plugin.ErrorMessage,
        plugin.InstallTime,
        plugin.IsBuiltIn
    };

    private static string FormatState(PluginState state)
    {
        var color = state switch
        {
            PluginState.Loaded or PluginState.Installed => "green",
            PluginState.Error => "red",
            PluginState.Disabled or PluginState.PendingUninstall or PluginState.PendingUpgrade => "yellow",
            _ => "grey"
        };
        return $"[{color}]{SpectreMarkup.Escape(state.ToString())}[/]";
    }
}

internal sealed record ConsoleCommandTree(
    RootCommand Root,
    Command Plugins,
    Command Plugin,
    Command PluginRun,
    Option<CliOutputFormat> Output,
    Argument<string?>? PluginAlias,
    Argument<string?>? PluginId,
    int RegisteredPluginCommands);

internal enum PluginInvocationRoute
{
    Direct,
    Explicit
}
