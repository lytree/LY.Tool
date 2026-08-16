using LYBox.Plugin.Shared.ViewModels;

namespace LYBox.Plugin.Shared;

public static class MenuItemTreeBuilder
{
    public static List<KeyValuePair<string?, MenuItemViewModel>> BuildTree(
        List<(string? Parent, MenuItemViewModel Item, int Order)> allItems)
    {
        var itemLookup = allItems.ToDictionary(x => x.Item.RawHeader ?? x.Item.MenuHeader, x => x.Item);

        var missingParents = allItems
            .Where(x => !string.IsNullOrEmpty(x.Parent) && !itemLookup.ContainsKey(x.Parent!))
            .Select(x => x.Parent)
            .Distinct()
            .ToList();

        foreach (var pHeader in missingParents)
        {
            // 父菜单图标去硬编码：从引用该父级的子菜单项继承图标（取首个非空 MenuIconName）。
            // 子项图标来自 [Menu(IconName=...)] 特性，新增父级分组无需再改中心代码。
            var iconName = allItems
                .Where(x => x.Parent == pHeader)
                .Select(x => x.Item.MenuIconName)
                .FirstOrDefault(n => !string.IsNullOrEmpty(n));

            var virtualParent = new MenuItemViewModel { MenuHeader = pHeader!, Key = pHeader!, MenuIconName = iconName };
            itemLookup[pHeader!] = virtualParent;
            allItems.Add((null, virtualParent, 0));
        }

        foreach (var entry in allItems)
        {
            if (!string.IsNullOrEmpty(entry.Parent) && itemLookup.TryGetValue(entry.Parent!, out var parentNode))
            {
                if (!parentNode.Children.Contains(entry.Item))
                {
                    parentNode.Children.Add(entry.Item);
                }
            }
        }

        return allItems
            .Where(x => string.IsNullOrEmpty(x.Parent))
            .OrderBy(x => x.Item.Order)
            .Select(x => new KeyValuePair<string?, MenuItemViewModel>(null, x.Item))
            .ToList();
    }
}
