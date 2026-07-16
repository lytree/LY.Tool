using System.Collections.ObjectModel;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.ViewModels;

namespace LYBox.UrsaWindow.ViewModels;

public class MenuViewModel : ViewModelBase
{
    public MenuViewModel()
    {
        MenuItems =
        [
            new() { MenuHeader = "NAV_Introduction", Key = "Introduction", IsSeparator = false },
        ];
    }

    public ObservableCollection<MenuItemViewModel> MenuItems { get; set; }

    public void RefreshHeaders()
    {
        foreach (var item in MenuItems)
            item.RefreshHeader();
    }
}
