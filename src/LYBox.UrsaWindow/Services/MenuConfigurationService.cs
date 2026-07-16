using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using LYBox.Plugin.Shared.ViewModels;
using LYBox.UrsaWindow.ViewModels;

namespace LYBox.UrsaWindow.Services;

public sealed class MenuConfigurationService : IMenuConfigurationService
{
    private readonly MenuViewModel _menuViewModel;
    private readonly ConcurrentDictionary<string, MenuItemViewModel> _menuItemsMap = new();
    private bool _mapBuilt;

    public MenuConfigurationService()
    {
        _menuViewModel = new MenuViewModel();
    }

    private void BuildMenuItemsMap(IEnumerable<MenuItemViewModel> menuItems)
    {
        foreach (var menuItem in menuItems)
        {
            if (!string.IsNullOrEmpty(menuItem.Key))
            {
                _menuItemsMap[menuItem.Key] = menuItem;
            }

            if (menuItem.Children is { Count: > 0 })
            {
                BuildMenuItemsMap(menuItem.Children);
            }
        }
    }

    public MenuViewModel GetMenuStructure()
    {
        if (!_mapBuilt)
        {
            BuildMenuItemsMap(_menuViewModel.MenuItems);
            _mapBuilt = true;
        }
        return _menuViewModel;
    }

    public void RegisterMenuItem(MenuItemViewModel menuItem, string? parentKey = null)
    {
        if (parentKey == null)
        {
            _menuViewModel.MenuItems.Add(menuItem);
        }
        else
        {
            var avaloniaParentMenuItem = FindAvaloniaMenuItem(parentKey);
            if (avaloniaParentMenuItem != null)
            {
                avaloniaParentMenuItem.Children ??= new();
                avaloniaParentMenuItem.Children.Add(menuItem);
            }
            if (!string.IsNullOrEmpty(menuItem.Key))
            {
                _menuItemsMap[menuItem.Key] = menuItem;
            }
        }
    }

    private MenuItemViewModel? FindAvaloniaMenuItem(string key)
    {
        if (_menuItemsMap.TryGetValue(key, out var mappedItem))
            return mappedItem;
        return FindAvaloniaMenuItemRecursive(_menuViewModel.MenuItems, key);
    }

    private static MenuItemViewModel? FindAvaloniaMenuItemRecursive(IEnumerable<MenuItemViewModel> menuItems, string key)
    {
        foreach (var menuItem in menuItems)
        {
            if (menuItem.Key == key)
                return menuItem;

            if (menuItem.Children != null)
            {
                var foundItem = FindAvaloniaMenuItemRecursive(menuItem.Children, key);
                if (foundItem != null)
                    return foundItem;
            }
        }
        return null;
    }

    public void RegisterMenuItems(List<KeyValuePair<string?, MenuItemViewModel>> menuItems)
    {
        foreach (var (parentKey, menuItem) in menuItems)
        {
            RegisterMenuItem(menuItem, parentKey);
        }
    }

    public void RemoveMenuItem(string key)
    {
        _menuItemsMap.TryRemove(key, out _);
        RemoveMenuItemFromParent(key);
    }

    private void RemoveMenuItemFromParent(string key)
    {
        var menuItemToRemove = FindAvaloniaMenuItem(key);
        if (menuItemToRemove != null && _menuViewModel.MenuItems.Remove(menuItemToRemove))
        {
            return;
        }

        foreach (var parentItem in _menuViewModel.MenuItems)
        {
            if (RemoveFromChildren(parentItem, key))
                return;
        }
    }

    private static bool RemoveFromChildren(MenuItemViewModel parentItem, string key)
    {
        if (parentItem.Children == null) return false;

        var menuItemToRemove = FindAvaloniaMenuItemRecursive(parentItem.Children, key);
        if (menuItemToRemove != null && parentItem.Children.Remove(menuItemToRemove))
            return true;

        foreach (var childItem in parentItem.Children)
        {
            if (RemoveFromChildren(childItem, key))
                return true;
        }

        return false;
    }

    public IEnumerable<string> GetMenuItemKeys()
    {
        return _menuItemsMap.Keys;
    }

    public MenuItemViewModel? GetMenuItemByKey(string key)
    {
        _menuItemsMap.TryGetValue(key, out var item);
        return item;
    }
}
