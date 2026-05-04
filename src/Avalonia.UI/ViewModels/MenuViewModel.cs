using System.Collections.ObjectModel;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.ViewModels;

namespace Avalonia.UI.ViewModels;

public class MenuViewModel : ViewModelBase
{
    public MenuViewModel()
    {
        MenuItems =
        [
            new() { MenuHeader = "Introduction", Key = "Introduction", IsSeparator = false },
            new() { MenuHeader = "Web 页面", Key = "WebPage", IsSeparator = false },
        ];
    }

    public ObservableCollection<MenuItemViewModel> MenuItems { get; set; }
}
