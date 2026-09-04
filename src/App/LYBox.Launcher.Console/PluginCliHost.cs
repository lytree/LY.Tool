using LYBox.Layout.Core.Services;
using LYBox.Layout.Ursa.Services;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.CommandLine;
using LYBox.Plugin.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace LYBox.Launcher.Console;

internal delegate Task<IPluginCliHost> PluginCliHostFactory(
    IAnsiConsole console,
    string? pluginsDirectory,
    CancellationToken cancellationToken);

internal interface IPluginCliHost : IAsyncDisposable
{
    Task LoadSelectedAsync(
        PluginCliSelection selection,
        bool suppressConsoleLogging,
        CancellationToken cancellationToken) => Task.CompletedTask;

    int RegisterCommands(
        System.CommandLine.Command pluginCommand,
        System.CommandLine.Command runCommand,
        PluginInvocationRoute route);
}

/// <summary>
/// Loads one resolved plugin and its dependency closure. Construction is intentionally
/// inert so catalog/help paths can never trigger assembly, database, or pending-operation work.
/// </summary>
internal sealed class PluginCliHost : IPluginCliHost
{
    private readonly IAnsiConsole _console;
    private readonly string? _pluginsDirectory;
    private PluginCliSelection? _selection;
    private PluginLoader? _pluginLoader;
    private ServiceProvider? _serviceProvider;

    private PluginCliHost(IAnsiConsole console, string? pluginsDirectory)
    {
        _console = console;
        _pluginsDirectory = pluginsDirectory;
    }

    public static Task<PluginCliHost> CreateAsync(
        IAnsiConsole console,
        string? pluginsDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(console);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new PluginCliHost(console, pluginsDirectory));
    }

    public async Task LoadSelectedAsync(
        PluginCliSelection selection,
        bool suppressConsoleLogging,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (_pluginLoader is not null)
            throw new InvalidOperationException("The selected plugin host has already been loaded.");

        var services = new ServiceCollection();
        ConfigureServices(services, _console, suppressConsoleLogging);

        var loader = PluginLoader.CreateTransient(
            selection.LoadOrder.Select(entry => entry.Info),
            _pluginsDirectory);
        ServiceProvider? provider = null;
        try
        {
            await loader.DiscoverAllPluginAssembliesAsync().ConfigureAwait(false);
            EnsureLoaded(selection, loader);
            await loader.InitializeAllPluginsAsync(services).ConfigureAwait(false);

            services.RemoveAll<PluginLoader>();
            services.AddSingleton(loader);
            services.RemoveAll<IPluginLoader>();
            services.AddSingleton<IPluginLoader>(loader);

            provider = services.BuildServiceProvider();
            ServiceLocator.Initialize(provider);
            PluginLoader.SetLogger(provider.GetRequiredService<ILogger<PluginLoader>>());

            if (selection.Profile == PluginCliExecutionProfile.SelectedPluginData)
            {
                await provider.GetRequiredService<DatabaseMigrationService>()
                    .MigrateAsync(cancellationToken).ConfigureAwait(false);
                if (provider.GetService<ISettingsService>() is SettingsService settings)
                    settings.InitializeDefaults();
            }

            await loader.RegisterAllPluginsAsync(provider).ConfigureAwait(false);
            EnsureLoaded(selection, loader);

            _selection = selection;
            _pluginLoader = loader;
            _serviceProvider = provider;
        }
        catch
        {
            if (provider is not null)
                await provider.DisposeAsync().ConfigureAwait(false);
            loader.Dispose();
            throw;
        }
    }

    internal static void ConfigureServices(
        IServiceCollection services,
        IAnsiConsole console,
        bool suppressConsoleLogging = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(console);

        services.AddAvaloniaServices();
        services.AddUrsaServices();
        services.AddSingleton(console);

        if (suppressConsoleLogging)
            services.AddLogging(builder => builder.ClearProviders());
    }

    public int RegisterCommands(
        System.CommandLine.Command pluginCommand,
        System.CommandLine.Command runCommand,
        PluginInvocationRoute route)
    {
        if (_pluginLoader is null || _serviceProvider is null || _selection is null)
            throw new InvalidOperationException("No selected plugin has been loaded.");

        var module = _pluginLoader.GetGeneratedModule(_selection.Target.Info.PluginId);
        var modules = module is IGeneratedPluginCliModule cliModule
            ? new[] { cliModule }
            : [];

        return route == PluginInvocationRoute.Explicit
            ? PluginCommandRegistry.RegisterExplicitCommands(
                runCommand,
                _serviceProvider,
                _console,
                modules,
                _selection.Target.Info.PluginId)
            : PluginCommandRegistry.RegisterCommands(
                pluginCommand,
                _serviceProvider,
                _console,
                modules,
                _selection.Target.Info.PluginId);
    }

    public async ValueTask DisposeAsync()
    {
        _pluginLoader?.Dispose();
        _pluginLoader = null;
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync().ConfigureAwait(false);
            _serviceProvider = null;
        }
    }

    private static void EnsureLoaded(PluginCliSelection selection, PluginLoader loader)
    {
        foreach (var entry in selection.LoadOrder)
        {
            var loaded = loader.GetPlugin(entry.Info.PluginId);
            if (loaded?.State != Plugin.Shared.Models.PluginState.Loaded)
            {
                throw new CliFailureException(
                    PluginCliExitCodes.PluginFailed,
                    "plugin_load_failed",
                    $"Plugin '{entry.Info.PluginId}' could not be loaded.",
                    new { pluginId = entry.Info.PluginId, error = loaded?.ErrorMessage });
            }
        }
    }
}
