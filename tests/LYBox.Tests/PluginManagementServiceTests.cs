using System.IO.Compression;
using System.Text.Json;
using LYBox.Layout.Core.Services;
using LYBox.Plugin.Shared.Models;
using LYBox.Plugin.Shared.Services;
using TUnit.Assertions;
using TUnit.Core;

namespace LYBox.Tests;

public class PluginManagementServiceTests
{
    private const string PluginId = "management.test";
    private const string AssemblyName = "Management.Test.dll";

    [Test]
    public async Task CreateDetachedAsync_PendingUninstall_DoesNotRunDesktopStartupCleanup()
    {
        using var workspace = new PluginWorkspace();
        var pluginDirectory = await workspace.WriteInstalledPluginAsync(
            PluginId,
            PluginState.PendingUninstall);

        using var service = await PluginManagementService.CreateDetachedAsync(workspace.PluginsDirectory);

        await Assert.That(Directory.Exists(pluginDirectory)).IsTrue();
        await Assert.That(service.GetPlugin(PluginId)?.State).IsEqualTo(PluginState.PendingUninstall);
    }

    [Test]
    public async Task InstallFromStream_ParentDirectoryPluginId_IsRejected()
    {
        using var workspace = new PluginWorkspace();
        using var loader = PluginLoader.CreateManagement([], workspace.PluginsDirectory);
        var manager = new PluginInstallationManager(loader, workspace.PluginsDirectory);
        await using var package = await workspace.CreatePackageAsync("..", "1.0.0");

        var result = await manager.InstallFromStreamAsync(package, "escape.zip");

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo(PluginManagementErrorCode.InvalidPackage);
        await Assert.That(File.Exists(Path.Combine(workspace.RootDirectory, "plugin.json"))).IsFalse();
    }

    [Test]
    public async Task InstallFromStream_ExistingPlugin_AlwaysStagesUpgrade()
    {
        using var workspace = new PluginWorkspace();
        var pluginDirectory = await workspace.WriteInstalledPluginAsync(PluginId, PluginState.Installed);
        var installedAssembly = Path.Combine(pluginDirectory, AssemblyName);
        await File.WriteAllTextAsync(installedAssembly, "old-version");
        var managed = PluginInventoryCatalog.ReadManagedPlugins(workspace.PluginsDirectory);
        using var loader = PluginLoader.CreateManagement(managed.Values, workspace.PluginsDirectory);
        var manager = new PluginInstallationManager(loader, workspace.PluginsDirectory);
        await using var package = await workspace.CreatePackageAsync(PluginId, "2.0.0");

        var result = await manager.InstallFromStreamAsync(package, "upgrade.zip");

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.PluginInfo?.State).IsEqualTo(PluginState.PendingUpgrade);
        await Assert.That(await File.ReadAllTextAsync(installedAssembly)).IsEqualTo("old-version");
        await Assert.That(File.Exists(Path.Combine(
            workspace.PluginsDirectory,
            ".pending",
            $"{PluginId}.new",
            AssemblyName))).IsTrue();
    }

    [Test]
    [NotInParallel]
    public async Task CreateDetachedAsync_ExternalPlugin_IsReadOnly()
    {
        using var workspace = new PluginWorkspace();
        var externalRoot = Path.Combine(workspace.RootDirectory, "external");
        Directory.CreateDirectory(externalRoot);
        await workspace.WriteManifestAsync(externalRoot, PluginId, "1.0.0", PluginState.Installed);
        File.Copy(typeof(PluginManagementServiceTests).Assembly.Location, Path.Combine(externalRoot, AssemblyName));
        var previous = Environment.GetEnvironmentVariable(PluginLoader.ExtraPluginEnvironmentVariableName);
        Environment.SetEnvironmentVariable(PluginLoader.ExtraPluginEnvironmentVariableName, externalRoot);

        try
        {
            using var service = await PluginManagementService.CreateDetachedAsync(workspace.PluginsDirectory);
            await using var package = await workspace.CreatePackageAsync(PluginId, "2.0.0");

            var install = await service.InstallFromFileAsync(await workspace.SavePackageAsync(package));
            var uninstall = await service.UninstallAsync(PluginId);

            await Assert.That(service.IsReadOnly(PluginId)).IsTrue();
            await Assert.That(service.CanUninstall(PluginId)).IsFalse();
            await Assert.That(install.ErrorCode).IsEqualTo(PluginManagementErrorCode.Conflict);
            await Assert.That(uninstall.ErrorCode).IsEqualTo(PluginManagementErrorCode.Conflict);
        }
        finally
        {
            Environment.SetEnvironmentVariable(PluginLoader.ExtraPluginEnvironmentVariableName, previous);
        }
    }

    private sealed class PluginWorkspace : IDisposable
    {
        public PluginWorkspace()
        {
            RootDirectory = Path.Combine(Path.GetTempPath(), "LYBox.Tests", Guid.NewGuid().ToString("N"));
            PluginsDirectory = Path.Combine(RootDirectory, "plugins");
            Directory.CreateDirectory(PluginsDirectory);
        }

        public string RootDirectory { get; }
        public string PluginsDirectory { get; }

        public async Task<string> WriteInstalledPluginAsync(string pluginId, PluginState state)
        {
            var directory = Path.Combine(PluginsDirectory, pluginId);
            Directory.CreateDirectory(directory);
            await WriteManifestAsync(directory, pluginId, "1.0.0", state);
            return directory;
        }

        public async Task WriteManifestAsync(
            string directory,
            string pluginId,
            string version,
            PluginState state)
        {
            var manifest = new PluginManifest
            {
                PluginId = pluginId,
                Name = "Management Test",
                Version = version,
                Assembly = AssemblyName,
                State = state.ToString()
            };
            await using var stream = File.Create(Path.Combine(directory, "plugin.json"));
            await JsonSerializer.SerializeAsync(stream, manifest, PluginUtilities.JsonOptions);
        }

        public async Task<MemoryStream> CreatePackageAsync(string pluginId, string version)
        {
            var package = new MemoryStream();
            using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
            {
                var manifestEntry = archive.CreateEntry("plugin.json");
                await using (var manifestStream = manifestEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(manifestStream, new PluginManifest
                    {
                        PluginId = pluginId,
                        Name = "Management Test",
                        Version = version,
                        Assembly = AssemblyName
                    }, PluginUtilities.JsonOptions);
                }

                var assemblyEntry = archive.CreateEntry(AssemblyName);
                await using var assemblyStream = assemblyEntry.Open();
                await using var source = File.OpenRead(typeof(PluginManagementServiceTests).Assembly.Location);
                await source.CopyToAsync(assemblyStream);
            }

            package.Position = 0;
            return package;
        }

        public async Task<string> SavePackageAsync(Stream package)
        {
            var path = Path.Combine(RootDirectory, "package.zip");
            await using var file = File.Create(path);
            package.Position = 0;
            await package.CopyToAsync(file);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(RootDirectory))
                Directory.Delete(RootDirectory, recursive: true);
        }
    }
}
