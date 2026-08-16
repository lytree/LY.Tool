using System.Collections.Generic;
using LYBox.Plugin.Shared;

namespace LYBox.Layout.Core.Services;

public interface INavigationService
{
    void RegisterNavigation(string key, ViewModelFactory factory);

    void RegisterNavigations(Dictionary<string, ViewModelFactory> navigations, string? pluginId = null);

    object CreateViewModel(string key);

    void InvalidateCache(string key);

    void InvalidateOwner(string? pluginId);

    void InvalidateAllCache();

    IEnumerable<string> GetNavigationKeys();
}
