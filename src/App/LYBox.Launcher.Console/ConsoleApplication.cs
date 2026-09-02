using System.CommandLine;
using System.Reflection;
using LYBox.Launcher.Desktop;
using LYBox.Plugin.Shared.CommandLine;
using LYBox.Plugin.Shared.Models;
using Spectre.Console;
using SpectreMarkup = Spectre.Console.Markup;

namespace LYBox.Launcher.Console;

internal sealed class ConsoleApplication
{
    private readonly IAnsiConsole _console;
    private readonly IAnsiConsole _errorConsole;
    private readonly TextWriter _standardOutput;
    private readonly TextWriter _standardError;
    private readonly Action<string[]> _startDesktop;
    private readonly string? _pluginsDirectory;
    private readonly PluginCliHostFactory _createPluginHost;

    public ConsoleApplication(IAnsiConsole console)
        : this(console, startDesktop: null, pluginsDirectory: null)
    {
    }

    public ConsoleApplication(
        IAnsiConsole console,
        Action<string[]>? startDesktop,
        string? pluginsDirectory = null,
        PluginCliHostFactory? createPluginHost = null,
        IAnsiConsole? errorConsole = null,
        TextWriter? standardOutput = null,
        TextWriter? standardError = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _errorConsole = errorConsole ?? console;
        _standardOutput = standardOutput ?? System.Console.Out;
        _standardError = standardError ?? System.Console.Error;
        _startDesktop = startDesktop ?? DesktopLauncher.StartWithConsole;
        _pluginsDirectory = pluginsDirectory;
        _createPluginHost = createPluginHost ?? CreatePluginHostAsync;
    }

    public async Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        CliOutput output;
        string[] normalizedArgs;
        try
        {
            var extracted = CliArguments.ExtractOutput(args);
            normalizedArgs = extracted.Arguments;
            output = new CliOutput(extracted.Format, _console, _errorConsole, _standardOutput);
        }
        catch (CliFailureException failure)
        {
            output = new CliOutput(CliOutputFormat.Text, _console, _errorConsole, _standardOutput);
            output.WriteFailure("cli", failure);
            return failure.ExitCode;
        }

        var commandName = GetCommandName(normalizedArgs);
        try
        {
            return CliInvocationClassifier.Classify(normalizedArgs) switch
            {
                PluginCliExecutionProfile.Desktop => await RunDesktopAsync(normalizedArgs, output).ConfigureAwait(false),
                PluginCliExecutionProfile.CatalogOnly => await RunCatalogAsync(normalizedArgs, output).ConfigureAwait(false),
                PluginCliExecutionProfile.SelectedPlugin => await RunSelectedPluginAsync(normalizedArgs, output).ConfigureAwait(false),
                _ => await InvokeAsync(CreateRootCommand(output, catalog: null), normalizedArgs).ConfigureAwait(false)
            };
        }
        catch (OperationCanceledException)
        {
            var failure = new CliFailureException(
                PluginCliExitCodes.Cancelled,
                "cancelled",
                "The operation was cancelled.");
            output.WriteFailure(commandName, failure);
            return failure.ExitCode;
        }
        catch (CliFailureException failure)
        {
            output.WriteFailure(commandName, failure);
            return failure.ExitCode;
        }
        catch (Exception exception)
        {
            output.WriteDiagnostic(exception.Message);
            var failure = new CliFailureException(
                PluginCliExitCodes.HostFailure,
                "host_error",
                "The CLI host failed to complete the command.");
            output.WriteFailure(commandName, failure);
            return failure.ExitCode;
        }
    }

    private static Task<IPluginCliHost> CreatePluginHostAsync(
        IAnsiConsole console,
        string? pluginsDirectory,
        CancellationToken cancellationToken) =>
        PluginCliHost.CreateAsync(console, pluginsDirectory, cancellationToken)
            .ContinueWith<IPluginCliHost>(task => task.GetAwaiter().GetResult(), cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    private async Task<int> RunDesktopAsync(string[] args, CliOutput output)
    {
        if (output.Format == CliOutputFormat.Json)
        {
            throw new CliFailureException(
                PluginCliExitCodes.Unsupported,
                "output_not_supported",
                "GUI launch does not support JSON output.");
        }

        if (args.Length == 0)
        {
            _startDesktop([]);
            return PluginCliExitCodes.Success;
        }

        return await InvokeAsync(CreateRootCommand(output, catalog: null), args).ConfigureAwait(false);
    }

    private async Task<int> RunCatalogAsync(string[] args, CliOutput output)
    {
        var catalog = new PluginManifestCatalog(_pluginsDirectory).Read();
        if (IsPluginHelpInvocation(args))
            return ShowPluginHelp(args, catalog, output);

        return await InvokeAsync(CreateRootCommand(output, catalog), args).ConfigureAwait(false);
    }

    private async Task<int> RunSelectedPluginAsync(string[] args, CliOutput output)
    {
        var invocation = ParsePluginInvocation(args);
        var catalog = new PluginManifestCatalog(_pluginsDirectory).Read();
        var target = invocation.IsLegacyAlias
            ? catalog.ResolveAlias(invocation.Target)
            : ResolveRunTarget(catalog, invocation.Target);

        ThrowIfInvalidSidecar(target);
        ThrowIfUnavailable(target);

        var loadOrder = catalog.ResolveDependencyOrder(target);
        foreach (var dependency in loadOrder) ThrowIfUnavailable(dependency);

        var profile = target.CliIndex?.GetRuntimeProfile() == PluginCliRuntimeProfile.Data
            || target.CliIndex is null
            ? PluginCliExecutionProfile.SelectedPluginData
            : PluginCliExecutionProfile.SelectedPlugin;
        var selection = new PluginCliSelection(target, loadOrder, profile);

        StringWriter? capturedOutput = null;
        var pluginConsole = _console;
        if (output.Format == CliOutputFormat.Json)
        {
            capturedOutput = new StringWriter();
            pluginConsole = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
                Out = new AnsiConsoleOutput(capturedOutput)
            });
        }

        await using var pluginHost = await _createPluginHost(
            pluginConsole,
            _pluginsDirectory,
            CancellationToken.None).ConfigureAwait(false);
        await pluginHost.LoadSelectedAsync(
            selection,
            suppressConsoleLogging: output.Format == CliOutputFormat.Json,
            CancellationToken.None).ConfigureAwait(false);

        var root = CreateExecutionRoot();
        var pluginNode = root.Subcommands.Single(command => command.Name == "plugin");
        var registered = pluginHost.RegisterCommands(pluginNode);
        if (registered == 0)
        {
            throw new CliFailureException(
                PluginCliExitCodes.NotFound,
                "plugin_command_not_found",
                $"Plugin '{target.Info.PluginId}' does not provide a CLI command.",
                new { pluginId = target.Info.PluginId });
        }
        if (registered != 1 || pluginNode.Subcommands.Count != 1)
        {
            throw new CliFailureException(
                PluginCliExitCodes.Conflict,
                "plugin_command_conflict",
                $"Plugin '{target.Info.PluginId}' provides more than one root CLI registrar.",
                new { pluginId = target.Info.PluginId, registered });
        }

        var commandName = pluginNode.Subcommands[0].Name;
        var executionArgs = new[] { "plugin", commandName }.Concat(invocation.CommandArguments).ToArray();
        var exitCode = await InvokeAsync(root, executionArgs, capturedOutput is null).ConfigureAwait(false);
        var text = capturedOutput?.ToString().TrimEnd();

        if (exitCode != PluginCliExitCodes.Success)
        {
            throw new CliFailureException(
                PluginCliExitCodes.PluginFailed,
                "plugin_failed",
                $"Plugin command failed with exit code {exitCode}.",
                new { pluginId = target.Info.PluginId, pluginExitCode = exitCode, output = text });
        }

        output.WriteSuccess("plugin.run", new
        {
            pluginId = target.Info.PluginId,
            alias = target.CliIndex?.Alias,
            output = text
        });
        return PluginCliExitCodes.Success;
    }

    private RootCommand CreateRootCommand(CliOutput output, PluginCatalogSnapshot? catalog)
    {
        var root = new RootCommand("LYBox desktop launcher and plugin command host")
        {
            TreatUnmatchedTokensAsErrors = true
        };
        root.Options.Add(CreateOutputOption());
        root.Subcommands.Add(CreateGuiCommand());
        root.Subcommands.Add(CreateVersionCommand(output));
        root.Subcommands.Add(CreatePluginsCommand(output, catalog));

        var plugin = new Command("plugin", "Run a CLI command provided by an installed plugin.");
        var target = new Argument<string>("plugin-id-or-alias")
        {
            Description = "Exact plugin id or alias from plugin.cli.json."
        };
        var arguments = new Argument<string[]>("arguments")
        {
            Arity = ArgumentArity.ZeroOrMore,
            Description = "Arguments forwarded to the selected plugin command."
        };
        var run = new Command("run", "Load one plugin and its dependencies, then run its CLI command.");
        run.Arguments.Add(target);
        run.Arguments.Add(arguments);
        plugin.Subcommands.Add(run);
        root.Subcommands.Add(plugin);
        return root;
    }

    private static RootCommand CreateExecutionRoot()
    {
        var root = new RootCommand { TreatUnmatchedTokensAsErrors = true };
        root.Subcommands.Add(new Command("plugin"));
        return root;
    }

    private static Option<string> CreateOutputOption()
    {
        return new Option<string>("--output")
        {
            Description = "Output format: text or json.",
            Arity = ArgumentArity.ExactlyOne,
            Recursive = true
        };
    }

    private Command CreateGuiCommand()
    {
        var arguments = new Argument<string[]>("arguments")
        {
            Description = "Arguments forwarded to the desktop launcher.",
            Arity = ArgumentArity.ZeroOrMore
        };
        var command = new Command("gui", "Start the LYBox desktop application.");
        command.Aliases.Add("desktop");
        command.Arguments.Add(arguments);
        command.SetAction(parseResult =>
        {
            _startDesktop(parseResult.GetValue(arguments) ?? []);
            return PluginCliExitCodes.Success;
        });
        return command;
    }

    private Command CreateVersionCommand(CliOutput output)
    {
        var command = new Command("version", "Display the launcher version.");
        command.SetAction(_ =>
        {
            var version = typeof(ConsoleApplication).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
                ?? typeof(ConsoleApplication).Assembly.GetName().Version?.ToString()
                ?? "unknown";
            if (output.Format == CliOutputFormat.Json)
                output.WriteSuccess("version", new { version });
            else
                _console.MarkupLine($"LYBox Launcher [green]{SpectreMarkup.Escape(version)}[/]");
            return PluginCliExitCodes.Success;
        });
        return command;
    }

    private Command CreatePluginsCommand(CliOutput output, PluginCatalogSnapshot? catalog)
    {
        var plugins = new Command("plugins", "Inspect installed plugin manifests without loading assemblies.");
        var list = new Command("list", "List installed plugins.");
        list.SetAction(_ => ListPlugins(catalog ?? new PluginManifestCatalog(_pluginsDirectory).Read(), output));
        plugins.Subcommands.Add(list);

        var pluginId = new Argument<string>("plugin-id")
        {
            Description = "Exact plugin id from plugin.json."
        };
        var info = new Command("info", "Display one installed plugin manifest.");
        info.Arguments.Add(pluginId);
        info.SetAction(parseResult => ShowPlugin(
            parseResult.GetRequiredValue(pluginId),
            catalog ?? new PluginManifestCatalog(_pluginsDirectory).Read(),
            output));
        plugins.Subcommands.Add(info);
        return plugins;
    }

    private int ListPlugins(PluginCatalogSnapshot catalog, CliOutput output)
    {
        var installed = catalog.Plugins
            .OrderBy(entry => entry.Info.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (output.Format == CliOutputFormat.Json)
        {
            output.WriteSuccess("plugins.list", new
            {
                plugins = installed.Select(entry => new
                {
                    id = entry.Info.PluginId,
                    entry.Info.Name,
                    entry.Info.Version,
                    state = entry.Info.State.ToString(),
                    cliAlias = entry.CliIndex?.Alias,
                    cliError = entry.CliIndexError
                }),
                diagnostics = catalog.Diagnostics
            });
        }
        else if (installed.Length == 0)
        {
            _console.MarkupLine("[yellow]No installed plugins were found.[/]");
        }
        else
        {
            var table = new Table()
                .Border(TableBorder.Simple)
                .AddColumn("Plugin")
                .AddColumn("Id")
                .AddColumn("Version")
                .AddColumn("State")
                .AddColumn("CLI alias");
            foreach (var entry in installed)
            {
                table.AddRow(
                    SpectreMarkup.Escape(entry.Info.Name),
                    SpectreMarkup.Escape(entry.Info.PluginId),
                    SpectreMarkup.Escape(entry.Info.Version),
                    FormatState(entry.Info.State),
                    SpectreMarkup.Escape(entry.CliIndex?.Alias ?? string.Empty));
            }
            _console.Write(table);
        }

        foreach (var diagnostic in catalog.Diagnostics) output.WriteDiagnostic(diagnostic);
        return catalog.Diagnostics.Count == 0
            ? PluginCliExitCodes.Success
            : PluginCliExitCodes.PartialSuccess;
    }

    private int ShowPlugin(string pluginId, PluginCatalogSnapshot catalog, CliOutput output)
    {
        var entry = catalog.ResolveExact(pluginId);
        if (output.Format == CliOutputFormat.Json)
        {
            output.WriteSuccess("plugins.info", new
            {
                id = entry.Info.PluginId,
                entry.Info.Name,
                entry.Info.Version,
                entry.Info.Author,
                entry.Info.Description,
                state = entry.Info.State.ToString(),
                entry.Info.Dependencies,
                assembly = entry.Info.AssemblyPath,
                installPath = entry.Info.InstallPath,
                cli = entry.CliIndex,
                cliError = entry.CliIndexError
            });
            return PluginCliExitCodes.Success;
        }

        var table = new Table().Border(TableBorder.Simple);
        table.AddColumn("Property");
        table.AddColumn("Value");
        table.AddRow("Name", SpectreMarkup.Escape(entry.Info.Name));
        table.AddRow("Id", SpectreMarkup.Escape(entry.Info.PluginId));
        table.AddRow("Version", SpectreMarkup.Escape(entry.Info.Version));
        table.AddRow("Author", SpectreMarkup.Escape(entry.Info.Author));
        table.AddRow("Description", SpectreMarkup.Escape(entry.Info.Description));
        table.AddRow("State", FormatState(entry.Info.State));
        table.AddRow("Assembly", SpectreMarkup.Escape(entry.Info.AssemblyPath));
        table.AddRow("Install path", SpectreMarkup.Escape(entry.Info.InstallPath));
        table.AddRow("CLI alias", SpectreMarkup.Escape(entry.CliIndex?.Alias ?? string.Empty));
        _console.Write(table);
        if (entry.CliIndexError is not null) output.WriteDiagnostic(entry.CliIndexError);
        return PluginCliExitCodes.Success;
    }

    private int ShowPluginHelp(string[] args, PluginCatalogSnapshot catalog, CliOutput output)
    {
        if (args.Length <= 2 && args.Skip(1).All(CliInvocationClassifier.IsHelpOption))
            return InvokeAsync(CreateRootCommand(output, catalog), ["plugin", "--help"]).GetAwaiter().GetResult();

        var invocation = ParsePluginInvocation(args, allowMissingCommand: true);
        var target = invocation.IsLegacyAlias
            ? catalog.ResolveAlias(invocation.Target)
            : ResolveRunTarget(catalog, invocation.Target);
        ThrowIfInvalidSidecar(target);

        if (output.Format == CliOutputFormat.Json)
        {
            output.WriteSuccess("plugin.help", new
            {
                pluginId = target.Info.PluginId,
                alias = target.CliIndex?.Alias,
                description = target.CliIndex?.Description ?? target.Info.Description,
                runtimeProfile = target.CliIndex?.RuntimeProfile ?? "data",
                outputModes = target.CliIndex?.OutputModes ?? ["text"],
                commands = target.CliIndex?.Commands ?? []
            });
        }
        else
        {
            _console.MarkupLine($"[bold]{SpectreMarkup.Escape(target.CliIndex?.Alias ?? target.Info.PluginId)}[/]");
            _console.WriteLine(target.CliIndex?.Description ?? target.Info.Description);
            _console.WriteLine($"Plugin: {target.Info.PluginId}");
            _console.WriteLine($"Runtime profile: {target.CliIndex?.RuntimeProfile ?? "data"}");
            if (target.CliIndex?.Commands.Count > 0)
            {
                foreach (var command in target.CliIndex.Commands)
                    _console.WriteLine($"  {command.Name}  {command.Description}");
            }
            else
            {
                _console.WriteLine("Command details are available only when the plugin sidecar declares them.");
            }
        }

        return PluginCliExitCodes.Success;
    }

    private async Task<int> InvokeAsync(RootCommand root, string[] args, bool forwardOutput = true)
    {
        var parseResult = root.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            throw new CliFailureException(
                PluginCliExitCodes.Usage,
                "invalid_arguments",
                string.Join(" ", parseResult.Errors.Select(error => error.Message)),
                new { errors = parseResult.Errors.Select(error => error.Message).ToArray() });
        }

        return await parseResult.InvokeAsync(new InvocationConfiguration
        {
            EnableDefaultExceptionHandler = false,
            Output = forwardOutput ? _standardOutput : TextWriter.Null,
            Error = forwardOutput ? _standardError : TextWriter.Null
        }).ConfigureAwait(false);
    }

    private static PluginCatalogEntry ResolveRunTarget(PluginCatalogSnapshot catalog, string target)
    {
        try
        {
            return catalog.ResolveExact(target);
        }
        catch (CliFailureException failure) when (failure.Code == "plugin_not_found")
        {
            return catalog.ResolveAlias(target);
        }
    }

    private static PluginInvocation ParsePluginInvocation(string[] args, bool allowMissingCommand = false)
    {
        if (args.Length >= 3
            && string.Equals(args[0], "plugin", StringComparison.OrdinalIgnoreCase)
            && string.Equals(args[1], "run", StringComparison.OrdinalIgnoreCase))
        {
            var forwarded = args.Skip(3).Where(value => !CliInvocationClassifier.IsHelpOption(value)).ToArray();
            if (!allowMissingCommand && forwarded.Length == 0)
                throw new CliFailureException(PluginCliExitCodes.Usage, "command_required", "A plugin command is required.");
            return new PluginInvocation(args[2], forwarded, IsLegacyAlias: false);
        }

        if (args.Length >= 2 && string.Equals(args[0], "plugin", StringComparison.OrdinalIgnoreCase))
        {
            var forwarded = args.Skip(2).Where(value => !CliInvocationClassifier.IsHelpOption(value)).ToArray();
            if (!allowMissingCommand && forwarded.Length == 0)
                throw new CliFailureException(PluginCliExitCodes.Usage, "command_required", "A plugin command is required.");
            return new PluginInvocation(args[1], forwarded, IsLegacyAlias: true);
        }

        throw new CliFailureException(PluginCliExitCodes.Usage, "invalid_arguments", "Invalid plugin invocation.");
    }

    private static void ThrowIfInvalidSidecar(PluginCatalogEntry entry)
    {
        if (entry.CliIndexError is not null)
        {
            throw new CliFailureException(
                PluginCliExitCodes.InvalidConfiguration,
                "invalid_cli_sidecar",
                entry.CliIndexError,
                new { pluginId = entry.Info.PluginId });
        }
    }

    private static void ThrowIfUnavailable(PluginCatalogEntry entry)
    {
        if (entry.Info.State is PluginState.Disabled or PluginState.PendingUninstall)
        {
            throw new CliFailureException(
                PluginCliExitCodes.Conflict,
                "plugin_unavailable",
                $"Plugin '{entry.Info.PluginId}' is {entry.Info.State}.",
                new { pluginId = entry.Info.PluginId, state = entry.Info.State.ToString() });
        }
        if (string.IsNullOrWhiteSpace(entry.Info.AssemblyPath) || !File.Exists(entry.Info.AssemblyPath))
        {
            throw new CliFailureException(
                PluginCliExitCodes.NotFound,
                "plugin_assembly_not_found",
                $"Plugin assembly for '{entry.Info.PluginId}' was not found.",
                new { pluginId = entry.Info.PluginId, assembly = entry.Info.AssemblyPath });
        }
    }

    private static bool IsPluginHelpInvocation(string[] args) =>
        args.Length > 0
        && string.Equals(args[0], "plugin", StringComparison.OrdinalIgnoreCase)
        && args.Skip(1).Any(CliInvocationClassifier.IsHelpOption);

    private static string GetCommandName(string[] args)
    {
        if (args.Length == 0) return "gui";
        if (args.Length >= 2) return $"{args[0]}.{args[1]}";
        return args[0];
    }

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

    private sealed record PluginInvocation(
        string Target,
        string[] CommandArguments,
        bool IsLegacyAlias);
}
