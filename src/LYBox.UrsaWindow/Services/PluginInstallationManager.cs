using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Models;
using LYBox.Plugin.Shared.Services;

namespace LYBox.UrsaWindow.Services;

public sealed class PluginInstallationManager : IPluginInstallationManager
{
    private readonly IPluginLoader _pluginLoader;
    private readonly string _pluginsDirectory;

    public event EventHandler<PluginInfo>? PluginInstalled;
    public event EventHandler<PluginInfo>? PluginUninstalled;
    public event EventHandler<PluginInfo>? PluginUpgradeScheduled;

    public PluginInstallationManager(IPluginLoader pluginLoader, string? pluginsDirectory = null)
    {
        _pluginLoader = pluginLoader;
        _pluginsDirectory = pluginsDirectory ?? Path.Combine(AppContext.BaseDirectory, "plugins");
        Directory.CreateDirectory(_pluginsDirectory);
    }

    public string GetPluginInstallDirectory() => _pluginsDirectory;

    public string GetPluginDirectory(string pluginId) => Path.Combine(_pluginsDirectory, pluginId);

    public async Task<PluginInstallResult> InstallFromFileAsync(string packageFilePath, IProgress<double>? progress = null)
    {
        if (!File.Exists(packageFilePath))
        {
            return new PluginInstallResult { Success = false, ErrorMessage = "Package file not found" };
        }

        if (!packageFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return new PluginInstallResult { Success = false, ErrorMessage = "Only .zip plugin packages are supported" };
        }

        await using var stream = File.OpenRead(packageFilePath);
        return await InstallFromStreamAsync(stream, Path.GetFileName(packageFilePath), progress);
    }

    public async Task<PluginInstallResult> InstallFromStreamAsync(Stream stream, string fileName, IProgress<double>? progress = null)
    {
        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return new PluginInstallResult { Success = false, ErrorMessage = "Only .zip plugin packages are supported" };
        }

        PluginInfo? pluginInfo = null;
        string? tempDir = null;

        try
        {
            tempDir = Path.Combine(Path.GetTempPath(), $"plugin_install_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
            {
                var entries = archive.Entries;
                var totalEntries = entries.Count;
                var processed = 0;

                foreach (var entry in entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    var destinationPath = Path.GetFullPath(Path.Combine(tempDir, entry.FullName));

                    if (!destinationPath.StartsWith(tempDir, StringComparison.OrdinalIgnoreCase))
                    {
                        return new PluginInstallResult
                        {
                            Success = false,
                            ErrorMessage = "Security: Path traversal detected in package"
                        };
                    }

                    var dir = Path.GetDirectoryName(destinationPath);
                    if (dir != null) Directory.CreateDirectory(dir);

                    entry.ExtractToFile(destinationPath, overwrite: true);

                    processed++;
                    progress?.Report((double)processed / totalEntries * 0.5);
                }
            }

            pluginInfo = await ParsePluginManifestAsync(tempDir);

            if (pluginInfo == null)
            {
                return new PluginInstallResult
                {
                    Success = false,
                    ErrorMessage = "Invalid plugin package: no valid plugin.json manifest found"
                };
            }

            // 修复 #11：安装时即校验 MinPluginSdkVersion，避免安装后启动失败。
            if (!PluginLoader.IsPluginSdkCompatible(pluginInfo.MinPluginSdkVersion))
            {
                var required = string.IsNullOrWhiteSpace(pluginInfo.MinPluginSdkVersion)
                    ? "0.0.0" : pluginInfo.MinPluginSdkVersion!;
                return new PluginInstallResult
                {
                    Success = false,
                    ErrorMessage = $"Plugin requires Plugin SDK >= {required}, but host provides " +
                                   $"{PluginSdkContract.CurrentVersion}. Update the host application " +
                                   "or contact the plugin author."
                };
            }

            var existingPlugin = _pluginLoader.GetPlugin(pluginInfo.PluginId);
            if (existingPlugin != null)
            {
                // 修复 #8 / 升级方案（docs/Plugin-Upgrade-Evaluation.md）：
                // 本项目不支持热卸载（见 AGENTS.md）。已加载插件的 DLL 被进程锁定，
                // 直接删除目录会失败（IOException）。改为：
                //   - Loaded/Error：调度升级（写入 .pending/，重启时迁移），不再拒绝
                //   - PendingUpgrade：覆盖 .pending/，更新 .upgrade.json 版本号（潜在问题 7）
                //   - 其他状态：尝试删除旧目录后正常安装
                if (existingPlugin.State == PluginState.Loaded ||
                    existingPlugin.State == PluginState.Error ||
                    existingPlugin.State == PluginState.PendingUpgrade)
                {
                    return await ScheduleUpgradeAsync(tempDir, pluginInfo, existingPlugin, progress);
                }

                var targetDir = GetPluginDirectory(pluginInfo.PluginId);
                if (Directory.Exists(targetDir))
                {
                    try
                    {
                        Directory.Delete(targetDir, true);
                    }
                    catch (Exception ex)
                    {
                        return new PluginInstallResult
                        {
                            Success = false,
                            ErrorMessage = $"Cannot remove previous install at '{targetDir}': {ex.Message}. " +
                                           "Close the application and try again."
                        };
                    }
                }
            }

            var installDir = GetPluginDirectory(pluginInfo.PluginId);
            Directory.CreateDirectory(installDir);

            var totalFiles = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories).Length;
            var copiedFiles = 0;

            foreach (var file in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(tempDir, file);
                var destPath = Path.GetFullPath(Path.Combine(installDir, relativePath));

                if (!destPath.StartsWith(installDir, StringComparison.OrdinalIgnoreCase))
                {
                    return new PluginInstallResult
                    {
                        Success = false,
                        ErrorMessage = "Security: Path traversal detected during installation"
                    };
                }

                var destDir = Path.GetDirectoryName(destPath);
                if (destDir != null) Directory.CreateDirectory(destDir);

                using var srcStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
                using var dstStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);
                await srcStream.CopyToAsync(dstStream, 65536);

                copiedFiles++;
                progress?.Report(0.5 + (double)copiedFiles / totalFiles * 0.5);
            }

            var mainAssembly = !string.IsNullOrEmpty(pluginInfo.AssemblyPath)
                ? Path.Combine(installDir, pluginInfo.AssemblyPath)
                : Directory.GetFiles(installDir, "*.dll", SearchOption.AllDirectories)
                    .FirstOrDefault(f => !f.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase));

            pluginInfo = pluginInfo.WithInstallInfo(installDir, mainAssembly ?? string.Empty, PluginState.Installed, DateTime.UtcNow);

            _pluginLoader.RegisterPlugin(pluginInfo);

            PluginInstalled?.Invoke(this, pluginInfo);

            return new PluginInstallResult { Success = true, PluginInfo = pluginInfo };
        }
        catch (Exception ex)
        {
            return new PluginInstallResult { Success = false, ErrorMessage = $"Installation failed: {ex.Message}" };
        }
        finally
        {
            if (tempDir != null && Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    public Task<bool> UninstallAsync(string pluginId)
    {
        var pluginInfo = _pluginLoader.GetPlugin(pluginId);
        if (pluginInfo == null) return Task.FromResult(false);

        if (pluginInfo.IsBuiltIn) return Task.FromResult(false);

        _pluginLoader.MarkForUninstall(pluginId);

        PluginUninstalled?.Invoke(this, pluginInfo);
        return Task.FromResult(true);
    }

    public Task<bool> CancelUpgradeAsync(string pluginId)
    {
        // 实现依据：docs/Plugin-Upgrade-Evaluation.md（潜在问题 1：重启前用户取消升级）
        var cancelled = _pluginLoader.CancelPendingUpgrade(pluginId);
        return Task.FromResult(cancelled);
    }

    /// <summary>
    /// 调度升级：把新版本（已解压在 tempDir）搬到 plugins/.pending/{PluginId}.new/，
    /// 写入 .upgrade.json，并通过 PluginLoader.MarkPendingUpgrade 把插件状态置为 PendingUpgrade。
    ///
    /// 实现依据：docs/Plugin-Upgrade-Evaluation.md
    ///   - 潜在问题 7（并发安装与升级）：若 .pending/{PluginId}.upgrade.json 已存在，
    ///     直接覆盖 .new/ 并更新 .upgrade.json 版本号，保证原子性。
    /// </summary>
    private async Task<PluginInstallResult> ScheduleUpgradeAsync(
        string tempDir,
        PluginInfo newPluginInfo,
        PluginInfo existingPlugin,
        IProgress<double>? progress)
    {
        var pendingDir = Path.Combine(_pluginsDirectory, ".pending");
        Directory.CreateDirectory(pendingDir);

        var newVersionDir = Path.Combine(pendingDir, $"{newPluginInfo.PluginId}.new");
        var upgradeJsonPath = Path.Combine(pendingDir, $"{newPluginInfo.PluginId}.upgrade.json");

        // 潜在问题 7：覆盖已存在的 .new/（用户连续点击两次升级）
        if (Directory.Exists(newVersionDir))
        {
            try { Directory.Delete(newVersionDir, true); }
            catch (Exception ex)
            {
                return new PluginInstallResult
                {
                    Success = false,
                    ErrorMessage = $"Cannot overwrite previous pending upgrade at '{newVersionDir}': {ex.Message}"
                };
            }
        }

        // 把整个 tempDir 移动到 .pending/{PluginId}.new/（原子性高于复制）
        try
        {
            Directory.Move(tempDir, newVersionDir);
            // tempDir 已被 Move 走，finally 块的 Directory.Exists 检查会安全跳过
        }
        catch (Exception ex)
        {
            // 跨卷场景 Move 可能失败，回退到复制+删除
            try
            {
                PluginUtilities.CopyDirectory(tempDir, newVersionDir);
            }
            catch (Exception copyEx)
            {
                return new PluginInstallResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to stage new version for upgrade (move: {ex.Message}; copy: {copyEx.Message})"
                };
            }
        }

        progress?.Report(1.0);

        // 写 .upgrade.json
        var upgradeInfo = new PendingUpgradeInfo
        {
            PluginId = newPluginInfo.PluginId,
            NewVersion = newPluginInfo.Version,
            ScheduledAt = DateTime.UtcNow,
            PreserveState = true,
            // 记录旧状态：Loaded 时迁移后应回到 Installed（不能直接 Loaded，必须重新加载）；
            // 其他状态按字面保留（仅 Disabled/Installed 合法）。
            OldStateToPreserve = existingPlugin.State.ToString(),
            NewVersionPath = newVersionDir
        };

        try
        {
            var json = JsonSerializer.Serialize(upgradeInfo, PluginUtilities.JsonOptions);
            await File.WriteAllTextAsync(upgradeJsonPath, json);
        }
        catch (Exception ex)
        {
            // 回滚：删除已就位的新版本目录
            try { Directory.Delete(newVersionDir, true); } catch { }
            return new PluginInstallResult
            {
                Success = false,
                ErrorMessage = $"Failed to write upgrade marker: {ex.Message}"
            };
        }

        // 让 PluginLoader 把内存中的插件状态改为 PendingUpgrade 并保存 manifest
        _pluginLoader.MarkPendingUpgrade(newPluginInfo.PluginId, upgradeInfo);

        var scheduledInfo = _pluginLoader.GetPlugin(newPluginInfo.PluginId);
        if (scheduledInfo != null)
        {
            PluginUpgradeScheduled?.Invoke(this, scheduledInfo);
        }

        return new PluginInstallResult
        {
            Success = true,
            PluginInfo = scheduledInfo ?? newPluginInfo
        };
    }

    public Task<bool> EnablePluginAsync(string pluginId)
    {
        _pluginLoader.EnablePlugin(pluginId);
        return Task.FromResult(true);
    }

    public Task<bool> DisablePluginAsync(string pluginId)
    {
        _pluginLoader.DisablePlugin(pluginId);
        return Task.FromResult(true);
    }

    private async Task<PluginInfo?> ParsePluginManifestAsync(string directory)
    {
        var manifestFile = Path.Combine(directory, "plugin.json");
        if (File.Exists(manifestFile))
        {
            var json = await File.ReadAllTextAsync(manifestFile);
            var manifest = JsonSerializer.Deserialize<PluginManifest>(json, PluginUtilities.JsonOptions);
            if (manifest != null)
            {
                return new PluginInfo
                {
                    PluginId = manifest.PluginId ?? string.Empty,
                    Name = manifest.Name ?? string.Empty,
                    Version = manifest.Version ?? "1.0.0",
                    Author = manifest.Author ?? string.Empty,
                    Description = manifest.Description ?? string.Empty,
                    Dependencies = manifest.Dependencies ?? [],
                    AssemblyPath = manifest.Assembly ?? string.Empty,
                    HasMetadata = !string.IsNullOrEmpty(manifest.PluginId),
                    MinPluginSdkVersion = manifest.MinPluginSdkVersion
                };
            }
        }

        var dllFiles = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories);
        foreach (var dll in dllFiles)
        {
            try
            {
                var assemblyName = System.Reflection.AssemblyName.GetAssemblyName(dll);
                return new PluginInfo
                {
                    PluginId = assemblyName.Name ?? Guid.NewGuid().ToString("N"),
                    Name = assemblyName.Name ?? "Unknown",
                    Version = assemblyName.Version?.ToString() ?? "1.0.0",
                    Author = "Unknown",
                    Description = string.Empty,
                    AssemblyPath = Path.GetRelativePath(directory, dll)
                };
            }
            catch
            {
            }
        }

        return null;
    }
}
