using Avalonia.Plugin.Shared.Models;

namespace Avalonia.Plugin.Shared.Services;

public interface IPluginLoader
{
    IReadOnlyList<PluginInfo> GetInstalledPlugins();
    PluginInfo? GetPlugin(string pluginId);
    Task<PluginLoadResult> LoadPluginAsync(PluginInfo pluginInfo);
    Task LoadAllPluginsAsync();
    IPlugin? GetLoadedPlugin(string pluginId);
    IPluginMetadata? GetLoadedMetadata(string pluginId);
    void RegisterPlugin(PluginInfo pluginInfo);
    void UnregisterPlugin(string pluginId);
    void EnablePlugin(string pluginId);
    void DisablePlugin(string pluginId);
    void MarkForUninstall(string pluginId);
    event EventHandler<PluginInfo>? PluginLoaded;
    event EventHandler<PluginInfo>? PluginUnloaded;
    event EventHandler<PluginInfo>? PluginStateChanged;
}

public class PluginLoadResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public IPlugin? Plugin { get; set; }
    public IPluginMetadata? Metadata { get; set; }
    public PluginInfo? PluginInfo { get; set; }
}
