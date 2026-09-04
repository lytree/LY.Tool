using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using LYBox.Layout.Core.Services;
using LYBox.Plugin.Shared.CommandLine;
using LYBox.Plugin.Shared.Models;
using Spectre.Console;
using SpectreMarkup = Spectre.Console.Markup;

namespace LYBox.Launcher.Console;

internal delegate Task<IPluginManagementService> PluginManagementServiceFactory(
    string? pluginsDirectory,
    CancellationToken cancellationToken);

internal sealed class ConsoleCommandRuntime
{
    private readonly IAnsiConsole _console;
    private readonly IAnsiConsole _errorConsole;
    private readonly TextWriter _standardOutput;
    private readonly TextWriter _standardError;
    private readonly Action<string[]> _startDesktop;
    private readonly string? _pluginsDirectory;
    private readonly PluginCliHostFactory _createPluginHost;
    private readonly PluginManagementServiceFactory _createPluginManagementService;
    private IPluginManagementService? _pluginManagementService;

    public ConsoleCommandRuntime(
        IAnsiConsole console,
        IAnsiConsole errorConsole,
        TextWriter standardOutput,
        TextWriter standardError,
        Action<string[]> startDesktop,
        string? pluginsDirectory,
        PluginCliHostFactory createPluginHost,
        PluginManagementServiceFactory createPluginManagementService)
    {
        _console = console;
        _errorConsole = errorConsole;
        _standardOutput = standardOutput;
        _standardError = standardError;
        _startDesktop = startDesktop;
        _pluginsDirectory = pluginsDirectory;
        _createPluginHost = createPluginHost;
        _createPluginManagementService = createPluginManagementService;
    }

    public async Task<int> RunAsync(string[] args)
    {
        var output = CreateOutput(CliOutputFormat.Text);
        ParseResult? currentParseResult = null;
        IPluginCliHost? pluginHost = null;
        try
        {
            var factory = new ConsoleCommandFactory(
                _console,
                _startDesktop,
                GetPluginManagementServiceAsync,
                CreateOutput);
            var bootstrapTree = factory.CreateBootstrapCommandTree();
            var bootstrapResult = bootstrapTree.Root.Parse(args);
            currentParseResult = bootstrapResult;
            output = CreateOutput(GetOutputFormat(bootstrapResult, bootstrapTree));
            if (bootstrapResult.Errors.Count > 0)
                return WriteUsageError(bootstrapResult, output);

            var pluginInvocation = GetPluginInvocation(bootstrapResult, bootstrapTree);
            if (pluginInvocation is null)
                return await InvokeAsync(bootstrapResult).ConfigureAwait(false);

            var catalog = new PluginManifestCatalog(_pluginsDirectory).Read();
            var target = pluginInvocation.Route == PluginInvocationRoute.Explicit
                ? catalog.ResolveExact(pluginInvocation.Target)
                : catalog.ResolveAlias(pluginInvocation.Target);
            ThrowIfInvalidSidecar(target);
            var outputFormat = GetOutputFormat(bootstrapResult, bootstrapTree);
            ValidateOutputMode(target, outputFormat);
            if (bootstrapResult.Action is HelpAction)
                return ShowPluginCatalogHelp(target, outputFormat);

            ThrowIfUnavailable(target);
            var loadOrder = catalog.ResolveDependencyOrder(target);
            foreach (var dependency in loadOrder)
                ThrowIfUnavailable(dependency);

            var profile = target.CliIndex?.GetRuntimeProfile() == PluginCliRuntimeProfile.Data
                || target.CliIndex is null
                ? PluginCliExecutionProfile.SelectedPluginData
                : PluginCliExecutionProfile.SelectedPlugin;
            var selection = new PluginCliSelection(target, loadOrder, profile);

            StringWriter? capturedOutput = null;
            var pluginConsole = _console;
            if (outputFormat == CliOutputFormat.Json)
            {
                capturedOutput = new StringWriter();
                pluginConsole = AnsiConsole.Create(new AnsiConsoleSettings
                {
                    Ansi = AnsiSupport.No,
                    ColorSystem = ColorSystemSupport.NoColors,
                    Out = new AnsiConsoleOutput(capturedOutput)
                });
            }

            pluginHost = await _createPluginHost(
                    pluginConsole,
                    _pluginsDirectory,
                    CancellationToken.None)
                .ConfigureAwait(false);
            await pluginHost.LoadSelectedAsync(
                    selection,
                    suppressConsoleLogging: outputFormat == CliOutputFormat.Json,
                    CancellationToken.None)
                .ConfigureAwait(false);

            var commandTree = factory.CreatePluginCommandTree(pluginHost, pluginInvocation);
            var registered = commandTree.RegisteredPluginCommands;
            if (registered == 0)
            {
                throw new CliFailureException(
                    PluginCliExitCodes.NotFound,
                    "plugin_command_not_found",
                    $"Plugin '{target.Info.PluginId}' does not provide a CLI command.",
                    new { pluginId = target.Info.PluginId });
            }
            if (registered != 1)
            {
                throw new CliFailureException(
                    PluginCliExitCodes.Conflict,
                    "plugin_command_conflict",
                    $"Plugin '{target.Info.PluginId}' provides more than one root CLI registrar.",
                    new { pluginId = target.Info.PluginId, registered });
            }

            var parseResult = commandTree.Root.Parse(args);
            currentParseResult = parseResult;
            output = CreateOutput(GetOutputFormat(parseResult, commandTree));
            if (parseResult.Errors.Count > 0)
                return WriteUsageError(parseResult, output);

            var exitCode = await InvokeAsync(parseResult, capturedOutput is null).ConfigureAwait(false);
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
        catch (OperationCanceledException)
        {
            var failure = new CliFailureException(
                PluginCliExitCodes.Cancelled,
                "cancelled",
                "The operation was cancelled.");
            output.WriteFailure(GetCommandName(currentParseResult), failure);
            return failure.ExitCode;
        }
        catch (CliFailureException failure)
        {
            output.WriteFailure(GetCommandName(currentParseResult), failure);
            return failure.ExitCode;
        }
        catch (Exception exception) when (pluginHost is not null)
        {
            output.WriteDiagnostic(exception.Message);
            var failure = new CliFailureException(
                PluginCliExitCodes.PluginFailed,
                "plugin_failed",
                "The plugin command host failed to complete the command.");
            output.WriteFailure(GetCommandName(currentParseResult), failure);
            return failure.ExitCode;
        }
        catch (Exception exception)
        {
            output.WriteDiagnostic(exception.Message);
            var failure = new CliFailureException(
                PluginCliExitCodes.HostFailure,
                "host_error",
                "The CLI host failed to complete the command.");
            output.WriteFailure(GetCommandName(currentParseResult), failure);
            return failure.ExitCode;
        }
        finally
        {
            if (pluginHost is not null)
                await pluginHost.DisposeAsync().ConfigureAwait(false);
            if (_pluginManagementService is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            else if (_pluginManagementService is IDisposable disposable)
                disposable.Dispose();
        }
    }

    private async Task<IPluginManagementService> GetPluginManagementServiceAsync(
        CancellationToken cancellationToken)
    {
        _pluginManagementService ??= await _createPluginManagementService(
                _pluginsDirectory,
                cancellationToken)
            .ConfigureAwait(false);
        return _pluginManagementService;
    }

    private static PluginBootstrapInvocation? GetPluginInvocation(
        ParseResult parseResult,
        ConsoleCommandTree commandTree)
    {
        if (parseResult.GetResult(commandTree.Plugin) is null)
            return null;

        if (parseResult.GetResult(commandTree.PluginRun) is not null)
        {
            var pluginIdResult = commandTree.PluginId is null
                ? null
                : parseResult.GetResult(commandTree.PluginId);
            return pluginIdResult is null || pluginIdResult.Tokens.Count == 0
                ? null
                : new PluginBootstrapInvocation(
                    PluginInvocationRoute.Explicit,
                    pluginIdResult.GetValueOrDefault<string>()!);
        }

        var pluginAliasResult = commandTree.PluginAlias is null
            ? null
            : parseResult.GetResult(commandTree.PluginAlias);
        return pluginAliasResult is null || pluginAliasResult.Tokens.Count == 0
            ? null
            : new PluginBootstrapInvocation(
                PluginInvocationRoute.Direct,
                pluginAliasResult.GetValueOrDefault<string>()!);
    }

    private static void ValidateOutputMode(
        PluginCatalogEntry target,
        CliOutputFormat outputFormat)
    {
        if (target.CliIndex is not null
            && !target.CliIndex.SupportsOutput(outputFormat.ToString()))
        {
            throw new CliFailureException(
                PluginCliExitCodes.Unsupported,
                "output_not_supported",
                $"Plugin '{target.Info.PluginId}' does not support {outputFormat.ToString().ToLowerInvariant()} output.");
        }
    }

    private int ShowPluginCatalogHelp(
        PluginCatalogEntry target,
        CliOutputFormat outputFormat)
    {
        var output = CreateOutput(outputFormat);
        if (outputFormat == CliOutputFormat.Json)
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
            return PluginCliExitCodes.Success;
        }

        _console.MarkupLine(
            $"[bold]{SpectreMarkup.Escape(target.CliIndex?.Alias ?? target.Info.PluginId)}[/]");
        _console.WriteLine(target.CliIndex?.Description ?? target.Info.Description);
        _console.WriteLine($"Plugin: {target.Info.PluginId}");
        _console.WriteLine($"Runtime profile: {target.CliIndex?.RuntimeProfile ?? "data"}");
        foreach (var command in target.CliIndex?.Commands ?? [])
            _console.WriteLine($"  {command.Name}  {command.Description}");
        return PluginCliExitCodes.Success;
    }

    private CliOutput CreateOutput(CliOutputFormat format) =>
        new(format, _console, _errorConsole, _standardOutput);

    private static CliOutputFormat GetOutputFormat(
        ParseResult parseResult,
        ConsoleCommandTree commandTree)
    {
        var optionResult = parseResult.GetResult(commandTree.Output);
        return optionResult is null || optionResult.Errors.Any()
            ? CliOutputFormat.Text
            : optionResult.GetValueOrDefault<CliOutputFormat>();
    }

    private async Task<int> InvokeAsync(ParseResult parseResult, bool forwardOutput = true) =>
        await parseResult.InvokeAsync(new InvocationConfiguration
        {
            EnableDefaultExceptionHandler = false,
            Output = forwardOutput ? _standardOutput : TextWriter.Null,
            Error = forwardOutput ? _standardError : TextWriter.Null
        }).ConfigureAwait(false);

    private static int WriteUsageError(ParseResult parseResult, CliOutput output)
    {
        var failure = new CliFailureException(
            PluginCliExitCodes.Usage,
            "invalid_arguments",
            string.Join(" ", parseResult.Errors.Select(error => error.Message)),
            new { errors = parseResult.Errors.Select(error => error.Message).ToArray() });
        output.WriteFailure(GetCommandName(parseResult), failure);
        return failure.ExitCode;
    }

    private static string GetCommandName(ParseResult? parseResult)
    {
        if (parseResult is null)
            return "cli";

        var commands = new Stack<string>();
        SymbolResult? current = parseResult.CommandResult;
        while (current is CommandResult commandResult)
        {
            if (commandResult.Command is not RootCommand)
                commands.Push(commandResult.Command.Name);
            current = commandResult.Parent;
        }

        return commands.Count == 0 ? "gui" : string.Join('.', commands);
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
}

internal sealed record PluginBootstrapInvocation(
    PluginInvocationRoute Route,
    string Target);
