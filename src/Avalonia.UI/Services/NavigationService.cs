using System.Collections.Generic;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Models;
using Avalonia.Plugin.Shared.Services;
using Avalonia.UI.Pages;
using Avalonia.UI.ViewModels;

namespace Avalonia.UI.Services;

public class NavigationService : INavigationService
{
    private readonly Dictionary<string, ViewModelFactory> _viewModelFactories = [];
    private readonly Dictionary<string, object> _viewModelCache = [];

    public NavigationService()
    {
        RegisterDefaultNavigations();
    }

    public void AttachPluginLoader(IPluginLoader pluginLoader)
    {
        pluginLoader.PluginUnloaded += OnPluginUnloaded;
    }

    private void OnPluginUnloaded(object? sender, PluginInfo pluginInfo)
    {
        InvalidateCache(pluginInfo.PluginId);
    }

    private void RegisterDefaultNavigations()
    {
        RegisterNavigation("Introduction", () => new IntroductionDemoViewModel());
        RegisterNavigation("AboutUs", () => new AboutUsDemoViewModel());
        RegisterNavigation("Settings", () => new SettingsPageViewModel(
            ServiceLocator.GetService<ISettingsService>(),
            ServiceLocator.GetService<ILocalizationService>()));
        RegisterNavigation("PluginManagement", () => new PluginManagementViewModel(
            ServiceLocator.GetService<IPluginLoader>(),
            ServiceLocator.GetService<IPluginInstallationManager>()));

        ViewLocator.Register<IntroductionDemoViewModel, IntroductionDemo>();
        ViewLocator.Register<AboutUsDemoViewModel, AboutUsDemo>();
        ViewLocator.Register<SettingsPageViewModel, SettingsPage>();
        ViewLocator.Register<PluginManagementViewModel, PluginManagementPage>();
    }

    public void RegisterNavigation(string key, ViewModelFactory factory)
    {
        _viewModelFactories[key] = factory;
    }

    public void RegisterNavigations(Dictionary<string, ViewModelFactory> navigations)
    {
        foreach (var (key, factory) in navigations)
        {
            _viewModelFactories[key] = factory;
        }
    }

    public object CreateViewModel(string key)
    {
        if (_viewModelCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (_viewModelFactories.TryGetValue(key, out var factory))
        {
            var viewModel = factory();
            _viewModelCache[key] = viewModel;
            return viewModel;
        }

        throw new System.ArgumentOutOfRangeException(nameof(key), key, null);
    }

    public void InvalidateCache(string key)
    {
        if (_viewModelCache.TryGetValue(key, out var viewModel))
        {
            ViewLocator.InvalidateViewCache(viewModel);
            (viewModel as IDisposable)?.Dispose();
        }
        _viewModelCache.Remove(key);
    }

    public void InvalidateAllCache()
    {
        foreach (var viewModel in _viewModelCache.Values)
        {
            ViewLocator.InvalidateViewCache(viewModel);
            (viewModel as IDisposable)?.Dispose();
        }
        _viewModelCache.Clear();
    }

    public IEnumerable<string> GetNavigationKeys()
    {
        return _viewModelFactories.Keys;
    }
}
