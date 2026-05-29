using Avalonia.Plugin.Shared.ViewModels;

namespace Avalonia.Plugin.Shared;

public static class MenuItemTreeBuilder
{
    public static List<KeyValuePair<string, MenuItemViewModel>> BuildTree(
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
            var virtualParent = new MenuItemViewModel { MenuHeader = pHeader!, Key = pHeader! };
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
            .Select(x => new KeyValuePair<string, MenuItemViewModel>(null, x.Item))
            .ToList();
    }
}
