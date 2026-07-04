using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Models;
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

        RegisterSettings(serviceProvider);
        return Task.CompletedTask;
    }

    /// <summary>将插件设置项注册到宿主设置页（ISettingsService），统一通过 SQLite 持久化</summary>
    private void RegisterSettings(IServiceProvider serviceProvider)
    {
        var settingsService = serviceProvider.GetService<ISettingsService>();
        if (settingsService == null) return;

        settingsService.RegisterSettings(
        [
            SettingDefinition.Path("DL.FfmpegPath", Strings.Get("LBL_FfmpegPath"), Strings.Get("HINT_FfmpegPath"),
                "Downloader", 10, 0, string.Empty, PluginId),
            SettingDefinition.Path("DL.Mp4DecryptPath", Strings.Get("LBL_Mp4DecryptPath"), Strings.Get("HINT_Mp4DecryptPath"),
                "Downloader", 10, 1, string.Empty, PluginId),
            SettingDefinition.Path("DL.MkvmergePath", Strings.Get("LBL_MkvmergePath"), Strings.Get("HINT_MkvmergePath"),
                "Downloader", 10, 2, string.Empty, PluginId),
            SettingDefinition.Path("DL.ShakaPackagerPath", Strings.Get("LBL_ShakaPath"), Strings.Get("HINT_ShakaPath"),
                "Downloader", 10, 3, string.Empty, PluginId),
            SettingDefinition.Text("DL.Proxy", Strings.Get("LBL_Proxy"), Strings.Get("HINT_Proxy"), Strings.Get("HINT_Proxy"),
                "Downloader", 10, 4, string.Empty, PluginId),
            SettingDefinition.Switch("DL.UseSystemProxy", Strings.Get("LBL_UseSystemProxy"), Strings.Get("DESC_UseSystemProxy"),
                "Downloader", 10, 5, true, PluginId),
            SettingDefinition.Text("DL.LogLevel", Strings.Get("LBL_LogLevel"), null, "",
                "Downloader", 10, 6, "INFO", PluginId),
        ]);
    }
}
