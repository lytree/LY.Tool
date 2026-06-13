using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.Shared.Services;
using Avalonia.Plugin.ProDataGrid.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace Avalonia.Plugin.ProDataGrid;

[GenerateMetadata]
public partial class ProDataGridPlugin : IPluginMetadata
{
    public string Name => "ProDataGrid Plugin";
    public string Version => "1.0.0";
    public string Author => "AvaloniaTemplate";
    public string Description => "ProDataGrid advanced data grid controls demo plugin.";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "0F2F7DB6-0E9B-D872-442F-2CBC3DAC1FA1";

    public Task InitializeAsync(IServiceCollection services) => Task.CompletedTask;

    public Task RegisterAsync(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService<ILocalizationService>() is { } loc)
            loc.RegisterResourceManager(Strings.ResourceManager);
        return Task.CompletedTask;
    }
}
