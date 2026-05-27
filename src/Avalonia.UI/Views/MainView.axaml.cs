using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Messaging;
using System;
using Ursa.Controls;
using Avalonia.UI.ViewModels;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Services;

namespace Avalonia.UI.Views;

public partial class MainView : UserControl
{
    private MainViewViewModel? _viewModel;
    private ILocalizationService? _localizationService;

    public MainView()
    {
        InitializeComponent();
        UpdateBreadcrumb("Introduction");
        WeakReferenceMessenger.Default.Register<string, string>(this, "JumpTo", OnNavigationChanged);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // 重新注册 Messenger（View 可能被 Detach 后再次 Attach）
        WeakReferenceMessenger.Default.Register<string, string>(this, "JumpTo", OnNavigationChanged);
        _viewModel = DataContext as MainViewViewModel;
        _localizationService = ServiceLocator.GetService<ILocalizationService>();
        _localizationService.CultureChanged += OnCultureChanged;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || _viewModel is null)
            return;
        _viewModel.NotificationManager = WindowNotificationManager.TryGetNotificationManager(topLevel, out var manager)
            ? manager
            : new WindowNotificationManager(topLevel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        WeakReferenceMessenger.Default.Unregister<string, string>(this, "JumpTo");
        if (_localizationService is not null)
            _localizationService.CultureChanged -= OnCultureChanged;
    }

    private void OnCultureChanged(object? sender, System.Globalization.CultureInfo culture)
    {
        if (_viewModel is not null)
            UpdateBreadcrumbFromCurrentContent();
    }

    private void OnNavigationChanged(object recipient, string message)
    {
        UpdateBreadcrumb(message);
    }

    private void UpdateBreadcrumb(string pageKey)
    {
        var breadcrumb = this.FindControl<Breadcrumb>("Breadcrumb");
        if (breadcrumb is null) return;
        breadcrumb.Items.Clear();
        breadcrumb.Items.Add(new BreadcrumbItem { Content = GetLocalizedString("BREADCRUMB_HOME", "Home"), IsReadOnly = true });
        breadcrumb.Items.Add(new BreadcrumbItem { Content = GetPageDisplayName(pageKey), IsReadOnly = true });
    }

    private void UpdateBreadcrumbFromCurrentContent()
    {
        var breadcrumb = this.FindControl<Breadcrumb>("Breadcrumb");
        if (breadcrumb is null || breadcrumb.Items.Count < 2) return;
        breadcrumb.Items.Clear();
        breadcrumb.Items.Add(new BreadcrumbItem { Content = GetLocalizedString("BREADCRUMB_HOME", "Home"), IsReadOnly = true });
        var lastKey = _viewModel?.Content?.GetType().Name.Replace("ViewModel", "");
        if (lastKey is not null)
            breadcrumb.Items.Add(new BreadcrumbItem { Content = GetPageDisplayName(lastKey), IsReadOnly = true });
    }

    private string GetLocalizedString(string key, string fallback)
    {
        return _localizationService?.GetString(key, fallback) ?? fallback;
    }

    private string GetPageDisplayName(string pageKey)
    {
        var resourceKey = $"NAV_{pageKey}";
        var result = _localizationService?.GetString(resourceKey, pageKey) ?? pageKey;
        if (result == resourceKey) return pageKey;
        return result;
    }

    private async void JumpToAbout(object? sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send("AboutUs", "JumpTo");
    }

    private async void OpenRepository(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        var launcher = top.Launcher;
        await launcher.LaunchUriAsync(new Uri("https://github.com/irihitech/Ursa.Avalonia"));
    }
}
