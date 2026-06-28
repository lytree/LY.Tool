using System.Collections.ObjectModel;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Models;
using Avalonia.Plugin.Shared.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avalonia.UI.ViewModels;

public partial class PluginManagementViewModel : ViewModelBase
{
    private readonly IPluginLoader _pluginLoader;
    private readonly IPluginInstallationManager _installationManager;
    private readonly ILocalizationService _localizationService;

    public ObservableCollection<PluginItemViewModel> Plugins { get; } = [];

    [ObservableProperty] private PluginItemViewModel? _selectedPlugin;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private double _installProgress;
    [ObservableProperty] private bool _isInstalling;
    [ObservableProperty] private bool _needsRestart;

    public PluginManagementViewModel(IPluginLoader pluginLoader, IPluginInstallationManager installationManager)
    {
        _pluginLoader = pluginLoader;
        _installationManager = installationManager;
        _localizationService = ServiceLocator.GetService<ILocalizationService>();

        _installationManager.PluginInstalled += OnPluginInstalled;
        _installationManager.PluginUninstalled += OnPluginUninstalled;
        _installationManager.PluginUpgradeScheduled += OnPluginUpgradeScheduled;
        _pluginLoader.PluginStateChanged += OnPluginStateChanged;

        RefreshPlugins();
    }

    [RelayCommand]
    private void RefreshPlugins()
    {
        Plugins.Clear();
        var installedPlugins = _pluginLoader.GetInstalledPlugins();
        foreach (var plugin in installedPlugins)
        {
            Plugins.Add(new PluginItemViewModel(plugin, _localizationService));
        }

        NeedsRestart = installedPlugins.Any(p =>
            p.State == PluginState.PendingUninstall || p.State == PluginState.PendingUpgrade);
    }

    [RelayCommand]
    private async Task InstallPluginAsync()
    {
        var storageProvider = Avalonia.Controls.TopLevel.GetTopLevel(
            Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null);

        if (storageProvider?.StorageProvider == null) return;

        var files = await storageProvider.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = _localizationService.GetString("SELECT_PLUGIN_PACKAGE", "Select Plugin Package"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new Avalonia.Platform.Storage.FilePickerFileType(_localizationService.GetString("PLUGIN_PACKAGE", "Plugin Package"))
                {
                    Patterns = ["*.zip"]
                }
            ]
        });

        if (files.Count == 0) return;

        var filePath = files[0].Path.LocalPath;
        IsInstalling = true;
        InstallProgress = 0;

        var progress = new Progress<double>(p => InstallProgress = p * 100);
        var result = await _installationManager.InstallFromFileAsync(filePath, progress);

        IsInstalling = false;

        if (result.Success)
        {
            // 区分新安装与升级调度两种场景的提示文案
            if (result.PluginInfo?.State == PluginState.PendingUpgrade)
            {
                StatusMessage = _localizationService.GetString(
                    "PLUGIN_UPGRADE_SCHEDULED",
                    "Plugin '{0}' upgrade scheduled, restart to apply",
                    result.PluginInfo?.Name ?? "");
            }
            else
            {
                StatusMessage = _localizationService.GetString(
                    "PLUGIN_INSTALLED_RESTART",
                    "Plugin '{0}' installed, restart to activate",
                    result.PluginInfo?.Name ?? "");
            }
            NeedsRestart = true;
        }
        else
        {
            StatusMessage = _localizationService.GetString("INSTALLATION_FAILED", "Installation failed: {0}", result.ErrorMessage ?? "");
        }
    }

    [RelayCommand]
    private async Task UninstallPluginAsync(PluginItemViewModel? pluginItem)
    {
        if (pluginItem == null || pluginItem.IsBuiltIn) return;

        var success = await _installationManager.UninstallAsync(pluginItem.PluginId);
        if (success)
        {
            pluginItem.UpdateFrom(_pluginLoader.GetPlugin(pluginItem.PluginId) ?? new PluginInfo { PluginId = pluginItem.PluginId, Name = pluginItem.Name, State = PluginState.PendingUninstall }, _localizationService);
            StatusMessage = _localizationService.GetString("PLUGIN_UNINSTALL_AFTER_RESTART", "Plugin '{0}' will be uninstalled after restart", pluginItem.Name);
            NeedsRestart = true;
        }
    }

    [RelayCommand]
    private async Task CancelUpgradeAsync(PluginItemViewModel? pluginItem)
    {
        if (pluginItem == null) return;

        var success = await _installationManager.CancelUpgradeAsync(pluginItem.PluginId);
        if (success)
        {
            var updated = _pluginLoader.GetPlugin(pluginItem.PluginId);
            if (updated != null)
            {
                pluginItem.UpdateFrom(updated, _localizationService);
            }
            StatusMessage = _localizationService.GetString(
                "PLUGIN_UPGRADE_CANCELLED",
                "Plugin '{0}' upgrade cancelled",
                pluginItem.Name);

            // 取消后可能不再需要重启
            var installed = _pluginLoader.GetInstalledPlugins();
            NeedsRestart = installed.Any(p =>
                p.State == PluginState.PendingUninstall || p.State == PluginState.PendingUpgrade);
        }
    }

    [RelayCommand]
    private void EnablePlugin(PluginItemViewModel? pluginItem)
    {
        if (pluginItem == null) return;
        _ = _installationManager.EnablePluginAsync(pluginItem.PluginId);
        StatusMessage = _localizationService.GetString("PLUGIN_ENABLE_RESTART", "Plugin '{0}' will be enabled after restart", pluginItem.Name);
        NeedsRestart = true;
    }

    [RelayCommand]
    private void DisablePlugin(PluginItemViewModel? pluginItem)
    {
        if (pluginItem == null) return;
        _ = _installationManager.DisablePluginAsync(pluginItem.PluginId);
        StatusMessage = _localizationService.GetString("PLUGIN_DISABLE_RESTART", "Plugin '{0}' will be disabled after restart", pluginItem.Name);
        NeedsRestart = true;
    }

    private void OnPluginInstalled(object? sender, PluginInfo e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var existing = Plugins.FirstOrDefault(p => p.PluginId == e.PluginId);
            if (existing != null)
            {
                existing.UpdateFrom(e, _localizationService);
            }
            else
            {
                Plugins.Add(new PluginItemViewModel(e, _localizationService));
            }
        });
    }

    private void OnPluginUninstalled(object? sender, PluginInfo e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var item = Plugins.FirstOrDefault(p => p.PluginId == e.PluginId);
            if (item != null)
            {
                var updatedInfo = _pluginLoader.GetPlugin(e.PluginId);
                if (updatedInfo != null)
                {
                    item.UpdateFrom(updatedInfo, _localizationService);
                }
            }
            NeedsRestart = true;
        });
    }

    private void OnPluginStateChanged(object? sender, PluginInfo e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var item = Plugins.FirstOrDefault(p => p.PluginId == e.PluginId);
            if (item != null)
            {
                item.UpdateFrom(e, _localizationService);
            }
            else
            {
                Plugins.Add(new PluginItemViewModel(e, _localizationService));
            }
        });
    }

    private void OnPluginUpgradeScheduled(object? sender, PluginInfo e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var item = Plugins.FirstOrDefault(p => p.PluginId == e.PluginId);
            if (item != null)
            {
                item.UpdateFrom(e, _localizationService);
            }
            NeedsRestart = true;
        });
    }

    public override void Dispose()
    {
        _installationManager.PluginInstalled -= OnPluginInstalled;
        _installationManager.PluginUninstalled -= OnPluginUninstalled;
        _installationManager.PluginUpgradeScheduled -= OnPluginUpgradeScheduled;
        _pluginLoader.PluginStateChanged -= OnPluginStateChanged;
        base.Dispose();
    }
}

public partial class PluginItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _pluginId = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _version = string.Empty;
    [ObservableProperty] private string _author = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private PluginState _state;
    [ObservableProperty] private bool _isBuiltIn;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _stateText = string.Empty;
    [ObservableProperty] private string _stateColor = "#808080";
    [ObservableProperty] private bool _canEnable;
    [ObservableProperty] private bool _canDisable;
    [ObservableProperty] private bool _canUninstall;
    [ObservableProperty] private bool _canCancelUpgrade;
    [ObservableProperty] private string? _pendingUpgradeVersion;

    private ILocalizationService? _localizationService;

    public PluginItemViewModel(PluginInfo info, ILocalizationService? localizationService = null)
    {
        _localizationService = localizationService;
        UpdateFrom(info, localizationService);
    }

    public void UpdateFrom(PluginInfo info, ILocalizationService? localizationService = null)
    {
        if (localizationService is not null)
            _localizationService = localizationService;

        PluginId = info.PluginId;
        Name = info.Name;
        Version = info.Version;
        Author = info.Author;
        Description = info.Description;
        State = info.State;
        IsBuiltIn = info.IsBuiltIn;
        ErrorMessage = info.ErrorMessage;

        (StateText, StateColor) = info.State switch
        {
            PluginState.Loaded => (_localizationService?.GetString("STATE_LOADED", "Loaded") ?? "Loaded", "#4CAF50"),
            PluginState.Installed => (_localizationService?.GetString("STATE_INSTALLED", "Installed (restart to load)") ?? "Installed (restart to load)", "#2196F3"),
            PluginState.Disabled => (_localizationService?.GetString("STATE_DISABLED", "Disabled") ?? "Disabled", "#FF9800"),
            PluginState.PendingUninstall => (_localizationService?.GetString("STATE_PENDING_UNINSTALL", "Pending Uninstall") ?? "Pending Uninstall", "#9C27B0"),
            PluginState.PendingUpgrade => (_localizationService?.GetString("STATE_PENDING_UPGRADE", "Pending Upgrade") ?? "Pending Upgrade", "#00BCD4"),
            PluginState.Error => (_localizationService?.GetString("STATE_ERROR", "Error") ?? "Error", "#F44336"),
            _ => (_localizationService?.GetString("STATE_NOT_INSTALLED", "Not Installed") ?? "Not Installed", "#808080")
        };

        CanEnable = info.State == PluginState.Disabled;
        CanDisable = info.State == PluginState.Loaded || info.State == PluginState.Installed;
        CanUninstall = !info.IsBuiltIn &&
                       info.State != PluginState.PendingUninstall &&
                       info.State != PluginState.PendingUpgrade;
        CanCancelUpgrade = info.State == PluginState.PendingUpgrade;
        PendingUpgradeVersion = info.State == PluginState.PendingUpgrade
            ? info.PendingUpgradeVersion
            : null;
    }
}
