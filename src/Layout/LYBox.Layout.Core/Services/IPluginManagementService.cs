using LYBox.Plugin.Shared.Models;
using LYBox.Plugin.Shared.Services;

namespace LYBox.Layout.Core.Services;

public interface IPluginManagementService
{
    IReadOnlyList<PluginInfo> GetInstalledPlugins();
    PluginInfo? GetPlugin(string pluginId);
    bool IsReadOnly(string pluginId);
    bool CanUninstall(string pluginId);
    Task<PluginInstallResult> InstallFromFileAsync(
        string packageFilePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
    Task<PluginUninstallResult> UninstallAsync(
        string pluginId,
        CancellationToken cancellationToken = default);
    Task<bool> CancelUpgradeAsync(string pluginId, CancellationToken cancellationToken = default);
    Task<bool> EnablePluginAsync(string pluginId, CancellationToken cancellationToken = default);
    Task<bool> DisablePluginAsync(string pluginId, CancellationToken cancellationToken = default);

    event EventHandler<PluginInfo>? PluginInstalled;
    event EventHandler<PluginInfo>? PluginUninstalled;
    event EventHandler<PluginInfo>? PluginUpgradeScheduled;
    event EventHandler<PluginInfo>? PluginStateChanged;
}

public sealed record PluginUninstallResult
{
    public bool Success { get; init; }
    public PluginManagementErrorCode ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public PluginInfo? PluginInfo { get; init; }
    public bool AlreadyPending { get; init; }

    public static PluginUninstallResult Succeeded(PluginInfo pluginInfo, bool alreadyPending = false) => new()
    {
        Success = true,
        PluginInfo = pluginInfo,
        AlreadyPending = alreadyPending
    };

    public static PluginUninstallResult Failed(PluginManagementErrorCode errorCode, string message) => new()
    {
        ErrorCode = errorCode,
        ErrorMessage = message
    };
}
