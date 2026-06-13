using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.Shared.Services;
using Avalonia.Plugin.NavigationMenus.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace Avalonia.Plugin.NavigationMenus;

[GenerateMetadata]
public partial class NavigationMenusPlugin : IPluginMetadata
{
    public string Name => "Navigation & Menus Plugin";
    public string Version => "1.0.0";
    public string Author => "AvaloniaTemplate";
    public string Description => "Navigation and menu controls demo plugin.";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "Avalonia.Plugin.NavigationMenus";

    public Task InitializeAsync(IServiceCollection services) => Task.CompletedTask;

    public Task RegisterAsync(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService<ILocalizationService>() is { } loc)
            loc.RegisterResourceManager(Strings.ResourceManager);
        return Task.CompletedTask;
    }
}
