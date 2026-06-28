using Avalonia.Plugin.Shared.Models;

namespace Avalonia.Plugin.Shared.Services;

public interface IPluginInstallationManager
{
    Task<PluginInstallResult> InstallFromFileAsync(string packageFilePath, IProgress<double>? progress = null);
    Task<PluginInstallResult> InstallFromStreamAsync(Stream stream, string fileName, IProgress<double>? progress = null);
    Task<bool> UninstallAsync(string pluginId);
    Task<bool> EnablePluginAsync(string pluginId);
    Task<bool> DisablePluginAsync(string pluginId);
    string GetPluginInstallDirectory();
    string GetPluginDirectory(string pluginId);

    /// <summary>
    /// 取消一个已调度的待升级任务。返回 false 表示该插件当前没有待升级。
    /// 实现依据：docs/Plugin-Upgrade-Evaluation.md（潜在问题 1：重启前用户取消升级）
    /// </summary>
    Task<bool> CancelUpgradeAsync(string pluginId);

    event EventHandler<PluginInfo>? PluginInstalled;
    event EventHandler<PluginInfo>? PluginUninstalled;

    /// <summary>用户触发"升级已加载插件"且 .pending 写入成功时触发，UI 据此显示"待升级"状态。</summary>
    event EventHandler<PluginInfo>? PluginUpgradeScheduled;
}

public class PluginInstallResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public PluginInfo? PluginInfo { get; set; }
}
