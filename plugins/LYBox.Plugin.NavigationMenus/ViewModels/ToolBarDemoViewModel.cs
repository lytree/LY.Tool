using System.Collections.ObjectModel;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.NavigationMenus.Pages;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Ursa.Controls;

namespace LYBox.Plugin.NavigationMenus.ViewModels;

[NavigationItem("ToolBar")]
[Menu("NAV_ToolBar", "ToolBar", "NAV_NavigationMenus")]
[ViewMap(typeof(ToolBarDemo))]
public partial class ToolBarDemoViewModel : ObservableObject
{
    public ObservableCollection<ToolBarItemViewModel> Items { get; set; }

    [ObservableProperty] private Orientation _panelOrientation = Orientation.Vertical;
    [ObservableProperty] private Orientation _toolBarOrientation = Orientation.Horizontal;

    partial void OnToolBarOrientationChanged(Orientation value)
    {
        if (value == Orientation.Horizontal)
        {
            PanelOrientation = Orientation.Vertical;
        }
        else
        {
            PanelOrientation = Orientation.Horizontal;
        }
    }

    public ToolBarDemoViewModel()
    {
        Items = new()
        {
            new ToolBarButtonItemViewModel { Content = "New", OverflowMode = OverflowMode.AsNeeded },
            new ToolBarButtonItemViewModel { Content = "Open" },
            new ToolBarButtonItemViewModel { Content = "Save1" },
            new ToolBarButtonItemViewModel { Content = "Save2" },
            new ToolBarSeparatorViewModel(),
            new ToolBarButtonItemViewModel { Content = "Save3" },
            new ToolBarButtonItemViewModel { Content = "Save4" },
            new ToolBarButtonItemViewModel { Content = "Save5" },
            new ToolBarButtonItemViewModel { Content = "Save6" },
            new ToolBarButtonItemViewModel { Content = "Save7" },
            new ToolBarSeparatorViewModel(),
            new ToolBarButtonItemViewModel { Content = "Save8" },
            new ToolBarCheckBoxItemViweModel { Content = "Bold" },
            new ToolBarCheckBoxItemViweModel { Content = "Italic", OverflowMode = OverflowMode.Never },
            new ToolBarComboBoxItemViewModel { Content = "Font Size", Items = ["10", "12", "14"] }
        };
    }
}

public abstract class ToolBarItemViewModel : ObservableObject
{
    public OverflowMode OverflowMode { get; set; }
}

public class ToolBarButtonItemViewModel : ToolBarItemViewModel
{
    public string? Content { get; set; }
    public ICommand? Command { get; set; }

    public ToolBarButtonItemViewModel()
    {
        Command = new AsyncRelayCommand(async () => { await OverlayMessageBox.ShowAsync(Content ?? string.Empty); });
    }
}

public class ToolBarCheckBoxItemViweModel : ToolBarItemViewModel
{
    public string? Content { get; set; }
    public bool IsChecked { get; set; }
    public ICommand? Command { get; set; }

    public ToolBarCheckBoxItemViweModel()
    {
        Command = new AsyncRelayCommand(async () => { await OverlayMessageBox.ShowAsync(Content ?? string.Empty); });
    }
}

public class ToolBarComboBoxItemViewModel : ToolBarItemViewModel
{
    public string? Content { get; set; }
    public ObservableCollection<string>? Items { get; set; }

    private string? _selectedItem;

    public string? SelectedItem
    {
        get => _selectedItem;
        set
        {
            SetProperty(ref _selectedItem, value);
            _ = OverlayMessageBox.ShowAsync(value ?? string.Empty);
        }
    }
}

public class ToolBarSeparatorViewModel : ToolBarItemViewModel
{
}





