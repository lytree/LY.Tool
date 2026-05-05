using System.Collections.ObjectModel;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.NavigationMenus.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Ursa.Controls;

namespace Avalonia.Plugin.NavigationMenus.ViewModels;

[NavigationItem("Breadcrumb")]
[Menu("Breadcrumb", "Breadcrumb", "Navigation & Menus")]
[ViewMap(typeof(BreadcrumbDemo))]
public partial class BreadcrumbDemoViewModel: ObservableObject
{
    public ObservableCollection<BreadcrumbDemoItem> Items1 { get; set; } =
    [
        new BreadcrumbDemoItem { Section = "Home", Icon = "Home" },
        new BreadcrumbDemoItem { Section = "Page 1", Icon = "Page" },
        new BreadcrumbDemoItem { Section = "Page 2", Icon = "Page" },
        new BreadcrumbDemoItem { Section = "Page 3", Icon = "Page" },
        new BreadcrumbDemoItem { Section = "Page 4", Icon = "Page", IsReadOnly = true }
    ];
}

public partial class BreadcrumbDemoItem: ObservableObject
{
    public string? Section { get; set; }
    public string? Icon { get; set; }
    [ObservableProperty] private bool _isReadOnly;
    
    public ICommand Command { get; set; }

    public BreadcrumbDemoItem()
    {
        Command = new AsyncRelayCommand(async () =>
        {
            await OverlayMessageBox.ShowAsync(Section ?? string.Empty);
        });
    }
}





