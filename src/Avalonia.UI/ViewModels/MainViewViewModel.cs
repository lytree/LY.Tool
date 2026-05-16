using Avalonia.Plugin.Shared;
using Avalonia.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WindowNotificationManager = Ursa.Controls.WindowNotificationManager;

namespace Avalonia.UI.ViewModels;

public partial class MainViewViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IMenuConfigurationService _menuConfigurationService;

    public WindowNotificationManager? NotificationManager { get; set; }
    public MenuViewModel Menus { get; }
    [ObservableProperty] private string? _settingText = "Settings";

    [ObservableProperty] private string? _pluginText = "Plugins";
    [ObservableProperty] private object? _content;
    [ObservableProperty] private bool _isCollapsed;
    [ObservableProperty] private bool _isSidebarHidden;
    [RelayCommand]
    public void ToggleSidebar()
    {
        IsSidebarHidden = !IsSidebarHidden;
    }
    [RelayCommand]
    public void Activate(string key)
    {
        OnNavigation(this, key);
    }

    public MainViewViewModel(INavigationService navigationService, IMenuConfigurationService menuConfigurationService)
    {
        _navigationService = navigationService;
        _menuConfigurationService = menuConfigurationService;
        Menus = _menuConfigurationService.GetMenuStructure();
        WeakReferenceMessenger.Default.Register<MainViewViewModel, string, string>(this, "JumpTo", OnNavigation);
        OnNavigation(this, "Introduction");
    }

    private void OnNavigation(MainViewViewModel vm, string s)
    {
        Content = _navigationService.CreateViewModel(s);
    }
    partial void OnIsCollapsedChanged(bool value)
    {
        SettingText = value ? null : "Settings";
        PluginText = value ? null : "Plugin";
    }
}
