using System.Collections.ObjectModel;
using LYBox.Plugin.Shared;

namespace LYBox.Plugin.Shared.ViewModels;

/// <summary>
/// 菜单视图模型，承载菜单项集合。
/// 默认不包含任何菜单项，由各布局项目（Ursa/Fluent）自行通过 IMenuConfigurationService 注册默认项。
/// </summary>
public class MenuViewModel : ViewModelBase
{
    public MenuViewModel()
    {
        MenuItems = [];
    }

    public ObservableCollection<MenuItemViewModel> MenuItems { get; set; }

    public void RefreshHeaders()
    {
        foreach (var item in MenuItems)
            item.RefreshHeader();
    }
}
