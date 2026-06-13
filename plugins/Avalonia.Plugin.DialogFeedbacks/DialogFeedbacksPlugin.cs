using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.Shared.Services;
using Avalonia.Plugin.DialogFeedbacks.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace Avalonia.Plugin.DialogFeedbacks;

[GenerateMetadata]
public partial class DialogFeedbacksPlugin : IPluginMetadata
{
    public string Name => "Dialog & Feedbacks Plugin";
    public string Version => "1.0.0";
    public string Author => "AvaloniaTemplate";
    public string Description => "Dialog and feedback controls demo plugin.";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "Avalonia.Plugin.DialogFeedbacks";

    public Task InitializeAsync(IServiceCollection services) => Task.CompletedTask;

    public Task RegisterAsync(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService<ILocalizationService>() is { } loc)
            loc.RegisterResourceManager(Strings.ResourceManager);
        return Task.CompletedTask;
    }
}
