using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Models;
using LYBox.Plugin.Shared.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Layout.Core.ViewModels;

/// <summary>
/// 插件列表项 ViewModel，承载单个插件的展示状态与操作可用性。
/// 提取到 Core 供 Ursa/Fluent 两个布局共享。
/// </summary>
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
