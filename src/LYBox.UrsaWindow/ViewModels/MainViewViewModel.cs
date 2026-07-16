using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;
using LYBox.Plugin.Shared.ViewModels;
using LYBox.UrsaWindow.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Avalonia.Threading;
using WindowNotificationManager = Ursa.Controls.WindowNotificationManager;

namespace LYBox.UrsaWindow.ViewModels;

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

    // 搜索防抖：避免每次击键都触发导航和过滤
    private DispatcherTimer? _searchDebounceTimer;

    partial void OnSearchTextChanged(string? value)
    {
        // 重启防抖计时器（300ms），仅当计时器到期后才执行搜索
        _searchDebounceTimer?.Stop();
        _searchDebounceTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(300),
            DispatcherPriority.Background, OnSearchDebounce);
        _searchDebounceTimer.Start();
    }

    private void OnSearchDebounce(object? sender, EventArgs e)
    {
        _searchDebounceTimer!.Stop();
        var value = SearchText;
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
        // 使用扁平索引 O(1) 查找菜单项，替代递归遍历
        SelectedMenuItem = _menuConfigurationService.GetMenuItemByKey(s);
    }

    partial void OnIsCollapsedChanged(bool value)
    {
        SettingText = value ? null : _localizationService?.GetString("NAV_Settings", "Settings");
        PluginText = value ? null : _localizationService?.GetString("NAV_Plugins", "Plugins");
    }

    public override void Dispose()
    {
        _searchDebounceTimer?.Stop();
        _searchDebounceTimer = null;
        WeakReferenceMessenger.Default.Unregister<string, string>(this, "JumpTo");
        if (_localizationService is not null)
            _localizationService.CultureChanged -= OnCultureChanged;
        base.Dispose();
    }
}
