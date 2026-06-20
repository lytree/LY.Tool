using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Services;
using Avalonia.Plugin.Shared.ViewModels;
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
    private ILocalizationService? _localizationService;

    public WindowNotificationManager? NotificationManager { get; set; }
    public MenuViewModel Menus { get; }
    [ObservableProperty] private string? _settingText;
    [ObservableProperty] private string? _pluginText;
    [ObservableProperty] private object? _content;
    [ObservableProperty] private bool _isCollapsed;
    [ObservableProperty] private bool _isSidebarHidden;
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private MenuItemViewModel? _selectedMenuItem;

    partial void OnSearchTextChanged(string? value)
    {
        // 搜索时自动跳转到首页，确保首页已创建并注册消息接收器
        if (!string.IsNullOrWhiteSpace(value) && Content is not IntroductionDemoViewModel)
        {
            OnNavigation(this, "Introduction");
        }
        // 将搜索文本发送到首页（IntroductionDemoViewModel），用于过滤工具卡片
        WeakReferenceMessenger.Default.Send(value ?? string.Empty, "SearchChanged");
    }

    [RelayCommand]
    public void ToggleSidebar()
    {
        IsSidebarHidden = !IsSidebarHidden;
    }

    [RelayCommand]
    public void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
    }

    [RelayCommand]
    public void Activate(string key)
    {
        WeakReferenceMessenger.Default.Send(key, "JumpTo");
    }

    public MainViewViewModel(INavigationService navigationService, IMenuConfigurationService menuConfigurationService)
    {
        _navigationService = navigationService;
        _menuConfigurationService = menuConfigurationService;
        Menus = _menuConfigurationService.GetMenuStructure();

        _localizationService = ServiceLocator.GetService<ILocalizationService>();
        if (_localizationService is not null)
            _localizationService.CultureChanged += OnCultureChanged;

        UpdateLocalizedStrings();
        Menus.RefreshHeaders();
        WeakReferenceMessenger.Default.Register<MainViewViewModel, string, string>(this, "JumpTo", OnNavigation);
        OnNavigation(this, "Introduction");
    }

    private void OnCultureChanged(object? sender, System.Globalization.CultureInfo culture)
    {
        UpdateLocalizedStrings();
        Menus.RefreshHeaders();
    }

    private void UpdateLocalizedStrings()
    {
        SettingText = _localizationService?.GetString("NAV_Settings", "Settings");
        PluginText = _localizationService?.GetString("NAV_Plugins", "Plugins");
    }

    partial void OnContentChanged(object? value)
    {
        if (value is IDisposable disposable && !ReferenceEquals(disposable, this))
        {
            _disposableContent = disposable;
        }
    }

    private IDisposable? _disposableContent;

    private void OnNavigation(MainViewViewModel vm, string s)
    {
        if (_disposableContent is not null && !ReferenceEquals(_disposableContent, this))
        {
            _disposableContent.Dispose();
            _disposableContent = null;
        }
        Content = _navigationService.CreateViewModel(s);
        // 同步左侧导航栏选中项
        SelectedMenuItem = FindMenuItemByKey(Menus.MenuItems, s);
    }

    /// <summary>
    /// 递归查找指定 Key 对应的菜单项
    /// </summary>
    private static MenuItemViewModel? FindMenuItemByKey(
        System.Collections.Generic.IEnumerable<MenuItemViewModel> items, string key)
    {
        foreach (var item in items)
        {
            if (!item.IsSeparator && string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                return item;

            if (item.Children.Count > 0)
            {
                var found = FindMenuItemByKey(item.Children, key);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }

    partial void OnIsCollapsedChanged(bool value)
    {
        SettingText = value ? null : _localizationService?.GetString("NAV_Settings", "Settings");
        PluginText = value ? null : _localizationService?.GetString("NAV_Plugins", "Plugins");
    }

    public override void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<string, string>(this, "JumpTo");
        if (_localizationService is not null)
            _localizationService.CultureChanged -= OnCultureChanged;
        base.Dispose();
    }
}
