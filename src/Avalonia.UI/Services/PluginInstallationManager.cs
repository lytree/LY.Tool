using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Plugin.Shared.Models;
using Avalonia.Plugin.Shared.Services;

namespace Avalonia.UI.Services;

public class PluginInstallationManager : IPluginInstallationManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IPluginLoader _pluginLoader;
    private readonly string _pluginsDirectory;

    public event EventHandler<PluginInfo>? PluginInstalled;
    public event EventHandler<PluginInfo>? PluginUninstalled;

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

            var existingPlugin = _pluginLoader.GetPlugin(pluginInfo.PluginId);
            if (existingPlugin != null)
            {
                var targetDir = GetPluginDirectory(pluginInfo.PluginId);
                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
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
            var manifest = JsonSerializer.Deserialize<PluginManifest>(json, JsonOptions);
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
