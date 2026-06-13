using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.Shared.Services;
using Avalonia.Plugin.ScottPlot.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace Avalonia.Plugin.ScottPlot;

[GenerateMetadata]
public partial class ScottPlotPlugin : IPluginMetadata
{
    public string Name => "ScottPlot Plugin";
    public string Version => "1.0.0";
    public string Author => "AvaloniaTemplate";
    public string Description => "ScottPlot charting and plotting controls demo plugin.";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "0F2F7DB6-0E9B-D872-442F-2CBC3DAC1FA0";

    public Task InitializeAsync(IServiceCollection services) => Task.CompletedTask;

    public Task RegisterAsync(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService<ILocalizationService>() is { } loc)
            loc.RegisterResourceManager(Strings.ResourceManager);
        return Task.CompletedTask;
    }
}
