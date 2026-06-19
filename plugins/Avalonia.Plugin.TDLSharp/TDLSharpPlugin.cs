using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.Shared.Models;
using Avalonia.Plugin.Shared.Services;
using Avalonia.Plugin.TDLSharp.Resources;
using Avalonia.Plugin.TDLSharp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Avalonia.Plugin.TDLSharp;

[GenerateMetadata]
public partial class TDLSharpPlugin : IPluginMetadata
{
    public string Name => "TDLSharp Plugin";
    public string Version => "1.0.0";
    public string Author => "TDLSharp";
    public string Description => "Telegram TDLib integration plugin providing batch forward, message export, media download and more.";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "A1B2C3D4-E5F6-7890-ABCD-TDLSHARP00001";

    public Task InitializeAsync(IServiceCollection services)
    {
        services.AddSingleton<TdlClientManager>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<TdlClientManager>();

            var (apiId, apiHash, proxyServer, proxyPort, enableProxy, tdlRootPath) = ResolveSettings(sp);

            return new TdlClientManager(logger, apiId, apiHash, proxyServer, proxyPort, enableProxy, tdlRootPath);
        });
        return Task.CompletedTask;
    }

    public Task RegisterAsync(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService<ILocalizationService>() is { } loc)
            loc.RegisterResourceManager(Strings.ResourceManager);

        RegisterSettings(serviceProvider);
        return Task.CompletedTask;
    }

    private void RegisterSettings(IServiceProvider serviceProvider)
    {
        var settingsService = serviceProvider.GetService<ISettingsService>();
        if (settingsService == null) return;

        settingsService.RegisterSettings(
        [
            SettingDefinition.Path("TDL.TdlRootPath", Strings.Get("SETTING_TdlRootPath"), Strings.Get("SETTING_TdlRootPathDesc"),
                "TDL", 0, 0, GetDefaultTdlRoot(), PluginId, isFolder: true),
            SettingDefinition.Text("TDL.ApiId", "API ID", "Telegram API ID", "","TDL", 0, 1,
                GetEnvDefault("tdl_api_id"), PluginId),
            SettingDefinition.Text("TDL.ApiHash", "API Hash", "Telegram API Hash", "","TDL", 0, 2,
                GetEnvDefault("tdl_api_hash"), PluginId),
            SettingDefinition.Text("TDL.ProxyServer", Strings.Get("SETTING_ProxyServer"), Strings.Get("SETTING_ProxyServerDesc"), "","TDL", 1, 0,
                "127.0.0.1", PluginId),
            SettingDefinition.Text("TDL.ProxyPort", Strings.Get("SETTING_ProxyPort"), Strings.Get("SETTING_ProxyPortDesc"),"", "TDL", 1, 1,
                "7897", PluginId),
            SettingDefinition.Switch("TDL.EnableProxy", Strings.Get("SETTING_EnableProxy"), Strings.Get("SETTING_EnableProxyDesc"), "TDL", 1, 2,
                true, PluginId),
        ]);
    }

    private (string apiId, string apiHash, string proxyServer, int proxyPort, bool enableProxy, string tdlRootPath) ResolveSettings(IServiceProvider serviceProvider)
    {
        string apiId = GetSettingValue(serviceProvider, "TDL.ApiId", "tdl_api_id", "");
        string apiHash = GetSettingValue(serviceProvider, "TDL.ApiHash", "tdl_api_hash", "");
        string proxyServer = GetSettingValue(serviceProvider, "TDL.ProxyServer", "tdl_proxy_server", "127.0.0.1");
        string proxyPortStr = GetSettingValue(serviceProvider, "TDL.ProxyPort", "tdl_proxy_port", "7897");
        string enableProxyStr = GetSettingValue(serviceProvider, "TDL.EnableProxy", "tdl_enable_proxy", "true");
        string tdlRootPath = GetSettingValue(serviceProvider, "TDL.TdlRootPath", "tdl_root_path", GetDefaultTdlRoot());

        int proxyPort = int.TryParse(proxyPortStr, out var port) ? port : 7897;
        bool enableProxy = bool.TryParse(enableProxyStr, out var enabled) && enabled;

        return (apiId, apiHash, proxyServer, proxyPort, enableProxy, tdlRootPath);
    }

    private static string GetDefaultTdlRoot()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".tdl");
    }

    private static string GetSettingValue(IServiceProvider serviceProvider, string settingKey, string envKey, string defaultValue)
    {
        var settingsService = serviceProvider.GetService<ISettingsService>();
        if (settingsService != null)
        {
            var value = settingsService.GetValue(settingKey);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return GetEnvDefault(envKey) ?? defaultValue;
    }

    private static string? GetEnvDefault(string key)
    {
        return Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User);
    }
}
