using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.Shared.Services;
using Avalonia.Plugin.Downloader.Resources;

namespace Avalonia.Plugin.Downloader;

[GenerateMetadata]
public partial class DownloaderPlugin : IPluginMetadata
{
    public string Name => "Downloader Plugin";
    public string Version => "1.0.0";
    public string Author => "Downloader";
    public string Description => "M3U8 video downloader plugin supporting AES-128, AES-128-ECB, CHACHA20 encryption and FFmpeg merge.";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "B2C3D4E5-F6A7-8901-BCDE-DOWNLOADER001";

    public void Initialize()
    {
        if (ServiceLocator.TryGetService<ILocalizationService>(out var loc) && loc is not null)
            loc.RegisterResourceManager(Strings.ResourceManager);
    }
}
