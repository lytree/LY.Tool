using System.Collections.ObjectModel;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Models;
using LYBox.Plugin.Shared.Services;
using LYBox.Layout.Core.Services;
using LYBox.Layout.Core.ViewModels;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ursa.Controls;

namespace LYBox.Layout.Ursa.ViewModels;

public partial class PluginManagementViewModel : ViewModelBase
{
    private readonly IPluginManagementService _pluginManagementService;
    private readonly ILocalizationService _localizationService;

    public ObservableCollection<PluginItemViewModel> Plugins { get; } = [];

    [ObservableProperty] private PluginItemViewModel? _selectedPlugin;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private double _installProgress;
    [ObservableProperty] private bool _isInstalling;
    [ObservableProperty] private bool _needsRestart;

    public PluginManagementViewModel(IPluginManagementService pluginManagementService)
    {
        _pluginManagementService = pluginManagementService;
        _localizationService = ServiceLocator.GetService<ILocalizationService>();

        _pluginManagementService.PluginInstalled += OnPluginInstalled;
        _pluginManagementService.PluginUninstalled += OnPluginUninstalled;
        _pluginManagementService.PluginUpgradeScheduled += OnPluginUpgradeScheduled;
        _pluginManagementService.PluginStateChanged += OnPluginStateChanged;

        RefreshPlugins();
    }

    [RelayCommand]
    private void RefreshPlugins()
    {
        Plugins.Clear();
        var installedPlugins = _pluginManagementService.GetInstalledPlugins();
        foreach (var plugin in installedPlugins)
        {
            Plugins.Add(new PluginItemViewModel(
                plugin,
                _localizationService,
                _pluginManagementService.IsReadOnly(plugin.PluginId)));
        }

        NeedsRestart = installedPlugins.Any(p =>
            p.State == PluginState.PendingUninstall ||
            p.State == PluginState.PendingUpgrade ||
            p.State == PluginState.Installed);
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
        var result = await _pluginManagementService.InstallFromFileAsync(filePath, progress);

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
            // 显示安装失败提示对话框，展示具体原因
            var reason = result.ErrorMessage ?? "";
            StatusMessage = _localizationService.GetString("INSTALLATION_FAILED", reason);
            var title = _localizationService.GetString("INSTALLATION_FAILED_TITLE", "Installation Failed");
            await OverlayMessageBox.ShowAsync(StatusMessage, title,
                icon: MessageBoxIcon.Error, button: MessageBoxButton.OK);
        }
    }

    [RelayCommand]
    private async Task UninstallPluginAsync(PluginItemViewModel? pluginItem)
    {
        if (pluginItem == null || !_pluginManagementService.CanUninstall(pluginItem.PluginId)) return;

        var result = await _pluginManagementService.UninstallAsync(pluginItem.PluginId);
        if (result.Success)
        {
            pluginItem.UpdateFrom(
                result.PluginInfo ?? new PluginInfo
                {
                    PluginId = pluginItem.PluginId,
                    Name = pluginItem.Name,
                    State = PluginState.PendingUninstall
                },
                _localizationService,
                isReadOnly: false);
            StatusMessage = _localizationService.GetString("PLUGIN_UNINSTALL_AFTER_RESTART", "Plugin '{0}' will be uninstalled after restart", pluginItem.Name);
            NeedsRestart = true;
        }
    }

    [RelayCommand]
    private async Task CancelUpgradeAsync(PluginItemViewModel? pluginItem)
    {
        if (pluginItem == null) return;

        var success = await _pluginManagementService.CancelUpgradeAsync(pluginItem.PluginId);
        if (success)
        {
            var updated = _pluginManagementService.GetPlugin(pluginItem.PluginId);
            if (updated != null)
            {
                pluginItem.UpdateFrom(
                    updated,
                    _localizationService,
                    _pluginManagementService.IsReadOnly(updated.PluginId));
            }
            StatusMessage = _localizationService.GetString(
                "PLUGIN_UPGRADE_CANCELLED",
                "Plugin '{0}' upgrade cancelled",
                pluginItem.Name);

            // 取消后可能不再需要重启
            var installed = _pluginManagementService.GetInstalledPlugins();
            NeedsRestart = installed.Any(p =>
                p.State == PluginState.PendingUninstall ||
                p.State == PluginState.PendingUpgrade ||
                p.State == PluginState.Installed);
        }
    }

    [RelayCommand]
    private void EnablePlugin(PluginItemViewModel? pluginItem)
    {
        if (pluginItem == null) return;
        _ = _pluginManagementService.EnablePluginAsync(pluginItem.PluginId);
        StatusMessage = _localizationService.GetString("PLUGIN_ENABLE_RESTART", "Plugin '{0}' will be enabled after restart", pluginItem.Name);
        NeedsRestart = true;
    }

    [RelayCommand]
    private void DisablePlugin(PluginItemViewModel? pluginItem)
    {
        if (pluginItem == null) return;
        _ = _pluginManagementService.DisablePluginAsync(pluginItem.PluginId);
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
                existing.UpdateFrom(e, _localizationService, _pluginManagementService.IsReadOnly(e.PluginId));
            }
            else
            {
                Plugins.Add(new PluginItemViewModel(
                    e,
                    _localizationService,
                    _pluginManagementService.IsReadOnly(e.PluginId)));
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
                var updatedInfo = _pluginManagementService.GetPlugin(e.PluginId);
                if (updatedInfo != null)
                {
                    item.UpdateFrom(
                        updatedInfo,
                        _localizationService,
                        _pluginManagementService.IsReadOnly(updatedInfo.PluginId));
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
                item.UpdateFrom(e, _localizationService, _pluginManagementService.IsReadOnly(e.PluginId));
            }
            else
            {
                Plugins.Add(new PluginItemViewModel(
                    e,
                    _localizationService,
                    _pluginManagementService.IsReadOnly(e.PluginId)));
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
                item.UpdateFrom(e, _localizationService, _pluginManagementService.IsReadOnly(e.PluginId));
            }
            NeedsRestart = true;
        });
    }

    public override void Dispose()
    {
        _pluginManagementService.PluginInstalled -= OnPluginInstalled;
        _pluginManagementService.PluginUninstalled -= OnPluginUninstalled;
        _pluginManagementService.PluginUpgradeScheduled -= OnPluginUpgradeScheduled;
        _pluginManagementService.PluginStateChanged -= OnPluginStateChanged;
        base.Dispose();
    }
}
