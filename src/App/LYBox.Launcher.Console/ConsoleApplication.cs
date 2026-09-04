using LYBox.Launcher.Desktop;
using Spectre.Console;

namespace LYBox.Launcher.Console;

internal sealed class ConsoleApplication
{
    private readonly ConsoleCommandRuntime _runtime;

    public ConsoleApplication(IAnsiConsole console)
        : this(console, startDesktop: null, pluginsDirectory: null)
    {
    }

    public ConsoleApplication(
        IAnsiConsole console,
        Action<string[]>? startDesktop,
        string? pluginsDirectory = null,
        PluginCliHostFactory? createPluginHost = null,
        PluginManagementServiceFactory? createPluginManagementService = null,
        IAnsiConsole? errorConsole = null,
        TextWriter? standardOutput = null,
        TextWriter? standardError = null)
    {
        ArgumentNullException.ThrowIfNull(console);

        _runtime = new ConsoleCommandRuntime(
            console,
            errorConsole ?? console,
            standardOutput ?? System.Console.Out,
            standardError ?? System.Console.Error,
            startDesktop ?? DesktopLauncher.StartWithConsole,
            pluginsDirectory,
            createPluginHost ?? CreatePluginHostAsync,
            createPluginManagementService ?? CreatePluginManagementService);
    }

    public Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return _runtime.RunAsync(args);
    }

    private static async Task<IPluginCliHost> CreatePluginHostAsync(
        IAnsiConsole console,
        string? pluginsDirectory,
        CancellationToken cancellationToken) =>
        await PluginCliHost.CreateAsync(console, pluginsDirectory, cancellationToken)
            .ConfigureAwait(false);

    private static LYBox.Layout.Core.Services.IPluginManagementService CreatePluginManagementService(
        string? pluginsDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return LYBox.Layout.Core.Services.PluginManagementService.CreateDetached(pluginsDirectory);
    }
}
