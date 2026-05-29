using System.Collections.Generic;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.ViewModels;
using Avalonia.UI.ViewModels;

namespace Avalonia.UI.Services;

public interface IMenuConfigurationService
{
    /// <summary>
    /// 获取完整的菜单结构
    /// </summary>
    /// <returns>菜单结构</returns>
    MenuViewModel GetMenuStructure();

    /// <summary>
    /// 注册菜单项
    /// </summary>
    /// <param name="menuItem">菜单项</param>
    /// <param name="parentKey">父菜单项键（可选）</param>
    void RegisterMenuItem(MenuItemViewModel menuItem, string? parentKey = null);

    /// <summary>
    /// 注册多个菜单项
    /// </summary>
    /// <param name="menuItems">菜单项列表，包含菜单项和其父菜单项键（可选）</param>
    void RegisterMenuItems(List<KeyValuePair<string?, MenuItemViewModel>> menuItems);

    /// <summary>
    /// 移除菜单项
    /// </summary>
    /// <param name="key">菜单项键</param>
    void RemoveMenuItem(string key);

    /// <summary>
    /// 获取所有菜单项键
    /// </summary>
    /// <returns>菜单项键集合</returns>
    IEnumerable<string> GetMenuItemKeys();
}