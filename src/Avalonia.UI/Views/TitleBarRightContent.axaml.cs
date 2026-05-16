using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Messaging;
using System;
using Ursa.Controls;

namespace Avalonia.UI.Views;

public partial class TitleBarRightContent : UserControl
{
    public TitleBarRightContent()
    {
        InitializeComponent();
        UpdateBreadcrumb("Introduction");
        WeakReferenceMessenger.Default.Register<string, string>(this, "JumpTo", OnNavigationChanged);
    }

    private void OnNavigationChanged(object recipient, string message)
    {
        UpdateBreadcrumb(message);
    }

    private void UpdateBreadcrumb(string pageName)
    {
        var breadcrumb = this.FindControl<Breadcrumb>("Breadcrumb");
        if (breadcrumb is null) return;
        breadcrumb.Items.Clear();
        breadcrumb.Items.Add(new BreadcrumbItem { Content = "Home", IsReadOnly = true });
        breadcrumb.Items.Add(new BreadcrumbItem { Content = pageName, IsReadOnly = true });
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

    private void ToggleSidebar(object? sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send("ToggleSidebar", "SidebarAction");
    }
}
