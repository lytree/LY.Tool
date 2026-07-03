using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Services;
using LYBox.Plugin.Downloader.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace LYBox.Plugin.Downloader;

[GenerateMetadata]
public partial class DownloaderPlugin : IPluginMetadata
{
    public string Name => "Downloader Plugin";
    public string Version => "1.0.0";
    public string Author => "Downloader";
    public string Description => "Pure C# HLS/DASH/MSS downloader (N_m3u8DL-RE compatible): VOD download, live record, decrypt & mux, tool settings. ffmpeg/mp4decrypt/mkvmerge/shaka-packager paths configurable.";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "B2C3D4E5-F6A7-8901-BCDE-DOWNLOADER001";

    public Task InitializeAsync(IServiceCollection services) => Task.CompletedTask;

    public Task RegisterAsync(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService<ILocalizationService>() is { } loc)
            loc.RegisterResourceManager(Strings.ResourceManager);
        return Task.CompletedTask;
    }
}
