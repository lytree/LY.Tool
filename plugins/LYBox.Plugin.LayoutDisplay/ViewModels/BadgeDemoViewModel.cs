using System.Collections.ObjectModel;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.LayoutDisplay.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LYBox.Plugin.LayoutDisplay.ViewModels;

[NavigationItem("KeyBadge")]
[Menu("NAV_Badge", "KeyBadge", "NAV_LayoutDisplay")]
[ViewMap(typeof(BadgeDemo))]
public partial class BadgeDemoViewModel: ViewModelBase
{
    [ObservableProperty] private string? _text = null;

    public ObservableCollection<AnchorItemViewModel> AnchorItems { get; } = new()
    {
        new AnchorItemViewModel { AnchorId = "Item1", Header = "Item 1" },
        new AnchorItemViewModel { AnchorId = "Item2", Header = "Item 2" },
        new AnchorItemViewModel { AnchorId = "Item3", Header = "Item 3" },
    };

    public BadgeDemoViewModel()
    {
        
    }

    [RelayCommand]
    public void ChangeText()
    {
        if (Text == null)
        {
            Text = DateTime.Now.ToShortDateString();
        }
        else
        {
            Text = null;
        }
    }
}

public partial class AnchorItemViewModel : ObservableObject
{
    [ObservableProperty] private string? _anchorId;
    [ObservableProperty] private string? _header;
    public ObservableCollection<AnchorItemViewModel>? Children { get; set; }
}





