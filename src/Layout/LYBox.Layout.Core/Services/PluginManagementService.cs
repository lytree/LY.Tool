using LYBox.Plugin.Shared.Models;
using LYBox.Plugin.Shared.Services;

namespace LYBox.Layout.Core.Services;

public sealed class PluginManagementService : IPluginManagementService, IDisposable
{
    private readonly IPluginLoader pluginLoader;
    private readonly IPluginInstallationManager installationManager;
    private readonly IReadOnlyDictionary<string, PluginInfo> readOnlyPlugins;
    private readonly IDisposable? ownedResource;

    public PluginManagementService(
        IPluginLoader pluginLoader,
        IPluginInstallationManager installationManager)
        : this(pluginLoader, installationManager, PluginInventoryCatalog.ReadExternalPlugins(), null)
    {
    }

    private PluginManagementService(
        IPluginLoader pluginLoader,
        IPluginInstallationManager installationManager,
        IReadOnlyDictionary<string, PluginInfo> readOnlyPlugins,
        IDisposable? ownedResource)
    {
        this.pluginLoader = pluginLoader;
        this.installationManager = installationManager;
        this.readOnlyPlugins = readOnlyPlugins;
        this.ownedResource = ownedResource;
    }

    public event EventHandler<PluginInfo>? PluginInstalled
    {
        add => installationManager.PluginInstalled += value;
        remove => installationManager.PluginInstalled -= value;
    }

    public event EventHandler<PluginInfo>? PluginUninstalled
    {
        add => installationManager.PluginUninstalled += value;
        remove => installationManager.PluginUninstalled -= value;
    }

    public event EventHandler<PluginInfo>? PluginUpgradeScheduled
    {
        add => installationManager.PluginUpgradeScheduled += value;
        remove => installationManager.PluginUpgradeScheduled -= value;
    }

    public event EventHandler<PluginInfo>? PluginStateChanged
    {
        add => pluginLoader.PluginStateChanged += value;
        remove => pluginLoader.PluginStateChanged -= value;
    }

    public static Task<PluginManagementService> CreateDetachedAsync(
        string? pluginsDirectory = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = Path.GetFullPath(pluginsDirectory ?? Path.Combine(AppContext.BaseDirectory, "plugins"));
        var managedPlugins = PluginInventoryCatalog.ReadManagedPlugins(root);
        var externalPlugins = PluginInventoryCatalog.ReadExternalPlugins();
        var loader = PluginLoader.CreateManagement(managedPlugins.Values, root);
        var manager = new PluginInstallationManager(
            loader,
            root,
            externalPlugins.Keys.ToHashSet(StringComparer.Ordinal));
        return Task.FromResult(new PluginManagementService(loader, manager, externalPlugins, loader));
    }

    public IReadOnlyList<PluginInfo> GetInstalledPlugins()
    {
        var loadedPlugins = pluginLoader.GetInstalledPlugins();
        var loadedIds = loadedPlugins.Select(plugin => plugin.PluginId).ToHashSet(StringComparer.Ordinal);
        return loadedPlugins
            .Concat(readOnlyPlugins.Values.Where(plugin => !loadedIds.Contains(plugin.PluginId)))
            .OrderBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(plugin => plugin.PluginId, StringComparer.Ordinal)
            .ToArray();
    }

    public PluginInfo? GetPlugin(string pluginId) =>
        pluginLoader.GetPlugin(pluginId)
        ?? (readOnlyPlugins.TryGetValue(pluginId, out var plugin) ? plugin : null);

    public bool IsReadOnly(string pluginId)
    {
        var plugin = GetPlugin(pluginId);
        return plugin is null || !IsManagedPlugin(plugin);
    }

    public bool CanUninstall(string pluginId)
    {
        var plugin = GetPlugin(pluginId);
        return plugin is not null
            && IsManagedPlugin(plugin)
            && !plugin.IsBuiltIn
            && plugin.State is not PluginState.PendingUninstall and not PluginState.PendingUpgrade;
    }

    public Task<PluginInstallResult> InstallFromFileAsync(
        string packageFilePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return installationManager.InstallFromFileAsync(Path.GetFullPath(packageFilePath), progress);
    }

    public async Task<PluginUninstallResult> UninstallAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var plugin = GetPlugin(pluginId);
        if (plugin is null)
        {
            return PluginUninstallResult.Failed(
                PluginManagementErrorCode.NotFound,
                $"Plugin not found: {pluginId}");
        }

        if (!IsManagedPlugin(plugin))
        {
            return PluginUninstallResult.Failed(
                PluginManagementErrorCode.Conflict,
                $"Plugin '{pluginId}' is provided by an external read-only plugin directory and cannot be uninstalled.");
        }

        if (plugin.State == PluginState.PendingUninstall)
            return PluginUninstallResult.Succeeded(plugin, alreadyPending: true);

        if (plugin.IsBuiltIn)
        {
            return PluginUninstallResult.Failed(
                PluginManagementErrorCode.Conflict,
                $"Built-in plugin '{pluginId}' cannot be uninstalled.");
        }

        if (!await installationManager.UninstallAsync(pluginId))
        {
            return PluginUninstallResult.Failed(
                PluginManagementErrorCode.HostError,
                $"Failed to schedule plugin '{pluginId}' for uninstall.");
        }

        return PluginUninstallResult.Succeeded(
            pluginLoader.GetPlugin(pluginId) ?? plugin.WithState(PluginState.PendingUninstall));
    }

    public Task<bool> CancelUpgradeAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return IsManagedPluginId(pluginId)
            ? installationManager.CancelUpgradeAsync(pluginId)
            : Task.FromResult(false);
    }

    public Task<bool> EnablePluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return IsManagedPluginId(pluginId)
            ? installationManager.EnablePluginAsync(pluginId)
            : Task.FromResult(false);
    }

    public Task<bool> DisablePluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return IsManagedPluginId(pluginId)
            ? installationManager.DisablePluginAsync(pluginId)
            : Task.FromResult(false);
    }

    public void Dispose() => ownedResource?.Dispose();

    private bool IsManagedPluginId(string pluginId)
    {
        var plugin = pluginLoader.GetPlugin(pluginId);
        return plugin is not null && IsManagedPlugin(plugin);
    }

    private bool IsManagedPlugin(PluginInfo plugin)
    {
        if (string.IsNullOrWhiteSpace(plugin.InstallPath))
            return !readOnlyPlugins.ContainsKey(plugin.PluginId);

        return PluginPathValidator.IsWithinDirectory(
            installationManager.GetPluginInstallDirectory(),
            plugin.InstallPath);
    }
}
