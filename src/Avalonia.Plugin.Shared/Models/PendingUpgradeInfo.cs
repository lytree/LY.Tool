namespace Avalonia.Plugin.Shared.Models;

/// <summary>
/// 描述一个待迁移的插件升级任务，序列化为 .pending/{PluginId}.upgrade.json。
/// 由 PluginInstallationManager.ScheduleUpgradeAsync 在用户触发"升级已加载插件"时写入，
/// 由 PluginLoader.ProcessPendingUpgrades 在应用启动早期消费。
///
/// 实现依据：docs/Plugin-Upgrade-Evaluation.md
/// </summary>
public class PendingUpgradeInfo
{
    /// <summary>目标插件 ID（与 plugins/{PluginId}/ 目录名一致）。</summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>新版本号（来自新包 plugin.json 的 version 字段）。</summary>
    public string NewVersion { get; set; } = string.Empty;

    /// <summary>调度时间（UTC），用于日志与诊断。</summary>
    public DateTime ScheduledAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 是否在迁移时保留旧 manifest 的状态（仅 Disabled/Installed 合法，
    /// Error/PendingUninstall 重置为 Installed）。
    /// </summary>
    public bool PreserveState { get; set; } = true;

    /// <summary>
    /// 待保留的旧状态值（由 InstallationManager 在调度时根据当前 PluginInfo.State 写入）。
    /// 迁移时直接信任此字段，避免重启前状态又被改变导致歧义。
    /// </summary>
    public string? OldStateToPreserve { get; set; }

    /// <summary>新版本解压目录的绝对路径（plugins/.pending/{PluginId}.new/）。</summary>
    public string NewVersionPath { get; set; } = string.Empty;
}
