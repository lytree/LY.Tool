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

    /// <summary>
    /// 将插件标记为待升级状态（PendingUpgrade），并写入 .pending/{PluginId}.upgrade.json。
    /// 调用方负责把新版本解压到 <paramref name="info"/>.NewVersionPath 指定的目录。
    /// 实现依据：docs/Plugin-Upgrade-Evaluation.md
    /// </summary>
    void MarkPendingUpgrade(string pluginId, PendingUpgradeInfo info);

    /// <summary>
    /// 取消待升级：删除 .pending/{PluginId}.upgrade.json 与 .pending/{PluginId}.new/ 目录，
    /// 并把插件状态恢复为 Installed（或保留 Disabled 等先前状态）。
    /// </summary>
    bool CancelPendingUpgrade(string pluginId);

    /// <summary>读取某个插件的待升级信息；不存在返回 null。</summary>
    PendingUpgradeInfo? GetPendingUpgrade(string pluginId);

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
