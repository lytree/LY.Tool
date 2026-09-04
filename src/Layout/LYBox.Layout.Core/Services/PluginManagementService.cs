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
        IPluginInstallationManager installationManager,
        IReadOnlyDictionary<string, PluginInfo> readOnlyPlugins)
        : this(pluginLoader, installationManager, readOnlyPlugins, null)
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

    public static PluginManagementService CreateDetached(
        string? pluginsDirectory = null)
    {
        var root = Path.GetFullPath(pluginsDirectory ?? Path.Combine(AppContext.BaseDirectory, "plugins"));
        var managedPlugins = PluginInventoryCatalog.ReadManagedPlugins(root);
        var externalPlugins = PluginInventoryCatalog.ReadExternalPlugins();
        var loader = PluginLoader.CreateManagement(managedPlugins.Values, root);
        var manager = new PluginInstallationManager(
            loader,
            root,
            externalPlugins.Keys.ToHashSet(StringComparer.Ordinal));
        return new PluginManagementService(loader, manager, externalPlugins, loader);
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

    public bool CanUninstall(string pluginId) =>
        ValidateUninstall(pluginId, out _) == UninstallOutcome.CanUninstall;

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

        switch (ValidateUninstall(pluginId, out var plugin))
        {
            case UninstallOutcome.NotFound:
                return PluginUninstallResult.Failed(
                    PluginManagementErrorCode.NotFound,
                    $"Plugin not found: {pluginId}");
            case UninstallOutcome.ExternalReadOnly:
                return PluginUninstallResult.Failed(
                    PluginManagementErrorCode.Conflict,
                    $"Plugin '{pluginId}' is provided by an external read-only plugin directory and cannot be uninstalled.");
            case UninstallOutcome.AlreadyPending:
                return PluginUninstallResult.Succeeded(plugin!, alreadyPending: true);
            case UninstallOutcome.BuiltIn:
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
            pluginLoader.GetPlugin(pluginId) ?? plugin!.WithState(PluginState.PendingUninstall));
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

    private enum UninstallOutcome
    {
        CanUninstall,
        NotFound,
        ExternalReadOnly,
        AlreadyPending,
        BuiltIn,
        PendingUpgrade
    }

    /// <summary>
    /// 卸载前置校验，供 <see cref="CanUninstall"/> 与 <see cref="UninstallAsync"/> 共享，避免规则漂移。
    /// 校验顺序需保持与 <see cref="UninstallAsync"/> 的历史返回语义一致。
    /// </summary>
    private UninstallOutcome ValidateUninstall(string pluginId, out PluginInfo? plugin)
    {
        plugin = GetPlugin(pluginId);
        if (plugin is null)
            return UninstallOutcome.NotFound;
        if (!IsManagedPlugin(plugin))
            return UninstallOutcome.ExternalReadOnly;
        if (plugin.State == PluginState.PendingUninstall)
            return UninstallOutcome.AlreadyPending;
        if (plugin.IsBuiltIn)
            return UninstallOutcome.BuiltIn;
        return plugin.State == PluginState.PendingUpgrade
            ? UninstallOutcome.PendingUpgrade
            : UninstallOutcome.CanUninstall;
    }
}
