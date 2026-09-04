using System.Text.Json;
using System.Text.RegularExpressions;
using LYBox.Layout.Core.Services;
using LYBox.Launcher.Console;
using LYBox.Plugin.Shared.CommandLine;
using LYBox.Plugin.Shared.Models;
using LYBox.Plugin.Shared.Services;
using Spectre.Console;
using TUnit.Assertions;
using TUnit.Core;

namespace LYBox.Tests;

public class ConsoleCommandRuntimeTests
{
    private static readonly PluginInfo SamplePlugin = new()
    {
        PluginId = "sample.plugin",
        Name = "Sample Plugin",
        Version = "1.2.3",
        Author = "Tests",
        Description = "Console management test plugin",
        State = PluginState.Installed,
        AssemblyPath = "Sample.Plugin.dll",
        InstallPath = "plugins/sample.plugin"
    };

    [Test]
    public async Task ConsoleApplication_PluginsList_OutputJson_UsesManagementService()
    {
        var service = new FakePluginManagementService(SamplePlugin);
        var application = CreateApplication(service, out _, out var standardOutput);

        var exitCode = await application.RunAsync(["plugins", "list", "--output=json"]);

        using var response = JsonDocument.Parse(standardOutput.ToString());
        var root = response.RootElement;
        await Assert.That(exitCode).IsEqualTo(PluginCliExitCodes.Success);
        await Assert.That(service.GetInstalledPluginsCalls).IsEqualTo(1);
        await Assert.That(root.GetProperty("ok").GetBoolean()).IsTrue();
        await Assert.That(root.GetProperty("command").GetString()).IsEqualTo("plugins.list");
        await Assert.That(root.GetProperty("data").GetProperty("plugins")[0]
            .GetProperty("id").GetString()).IsEqualTo(SamplePlugin.PluginId);
    }

    [Test]
    public async Task ConsoleApplication_PluginsInfo_OutputJson_UsesManagementService()
    {
        var service = new FakePluginManagementService(SamplePlugin);
        var application = CreateApplication(service, out _, out var standardOutput);

        var exitCode = await application.RunAsync(
            ["plugins", "info", SamplePlugin.PluginId, "--output=json"]);

        using var response = JsonDocument.Parse(standardOutput.ToString());
        await Assert.That(exitCode).IsEqualTo(PluginCliExitCodes.Success);
        await Assert.That(service.RequestedPluginId).IsEqualTo(SamplePlugin.PluginId);
        await Assert.That(response.RootElement.GetProperty("command").GetString())
            .IsEqualTo("plugins.info");
    }

    [Test]
    [Arguments("install")]
    [Arguments("add")]
    public async Task ConsoleApplication_PluginsInstallAliases_OutputJson_UseManagementService(
        string commandName)
    {
        var service = new FakePluginManagementService(SamplePlugin)
        {
            InstallResult = new PluginInstallResult
            {
                Success = true,
                PluginInfo = SamplePlugin
            }
        };
        var application = CreateApplication(service, out _, out var standardOutput);

        var exitCode = await application.RunAsync(
            ["plugins", commandName, "sample.zip", "--output=json"]);

        using var response = JsonDocument.Parse(standardOutput.ToString());
        await Assert.That(exitCode).IsEqualTo(PluginCliExitCodes.Success);
        await Assert.That(service.InstalledPackagePath).IsEqualTo("sample.zip");
        await Assert.That(response.RootElement.GetProperty("command").GetString())
            .IsEqualTo("plugins.install");
    }

    [Test]
    [Arguments("uninstall")]
    [Arguments("remove")]
    public async Task ConsoleApplication_PluginsUninstallAliases_OutputJson_UseManagementService(
        string commandName)
    {
        var service = new FakePluginManagementService(SamplePlugin)
        {
            UninstallResult = PluginUninstallResult.Succeeded(
                SamplePlugin.WithState(PluginState.PendingUninstall))
        };
        var application = CreateApplication(service, out _, out var standardOutput);

        var exitCode = await application.RunAsync(
            ["plugins", commandName, SamplePlugin.PluginId, "--output=json"]);

        using var response = JsonDocument.Parse(standardOutput.ToString());
        await Assert.That(exitCode).IsEqualTo(PluginCliExitCodes.Success);
        await Assert.That(service.UninstalledPluginId).IsEqualTo(SamplePlugin.PluginId);
        await Assert.That(response.RootElement.GetProperty("command").GetString())
            .IsEqualTo("plugins.uninstall");
    }

    [Test]
    public async Task ConsoleApplication_OnlyInitializesAndDelegatesToRuntime()
    {
        var source = await ReadConsoleSourceAsync("ConsoleApplication.cs");

        await Assert.That(source).Contains("ConsoleCommandRuntime");
        await Assert.That(source).DoesNotContain("System.CommandLine");
        await Assert.That(source).DoesNotContain("new RootCommand");
        await Assert.That(source).DoesNotContain("PluginManifestCatalog");
    }

    [Test]
    public async Task ConsoleRuntimeAndFactory_UseOnlySystemCommandLineParsing()
    {
        var source = string.Join(
            Environment.NewLine,
            await ReadConsoleSourceAsync("ConsoleCommandRuntime.cs"),
            await ReadConsoleSourceAsync("ConsoleCommandFactory.cs"));

        await Assert.That(Regex.IsMatch(source, @"\bargs\s*\[")).IsFalse();
        await Assert.That(Regex.IsMatch(source, @"\bargs\s*\.\s*Length")).IsFalse();
        await Assert.That(Regex.IsMatch(source, @"\bargs\s*\.\s*Skip\s*\(")).IsFalse();
        await Assert.That(Regex.IsMatch(source, @"StartsWith\s*\(\s*""--output=")).IsFalse();
        foreach (var forbidden in new[]
                 {
                     "CliArguments",
                     "CliInvocationClassifier",
                     "NormalizePluginRunInvocation",
                     "ParsePluginInvocation",
                     "RequiresPluginHost",
                     "IsPluginHelpInvocation"
                 })
        {
            await Assert.That(source).DoesNotContain(forbidden);
        }
    }

    private static ConsoleApplication CreateApplication(
        IPluginManagementService service,
        out StringWriter consoleOutput,
        out StringWriter standardOutput)
    {
        var console = CreateConsole(out consoleOutput);
        standardOutput = new StringWriter();
        return new ConsoleApplication(
            console,
            _ => { },
            Path.Combine(Path.GetTempPath(), "LYBox.Tests", Guid.NewGuid().ToString("N")),
            createPluginManagementService: (_, _) => service,
            standardOutput: standardOutput,
            standardError: new StringWriter());
    }

    private static IAnsiConsole CreateConsole(out StringWriter output)
    {
        output = new StringWriter();
        return AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(output)
        });
    }

    private static async Task<string> ReadConsoleSourceAsync(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (!File.Exists(Path.Combine(directory.FullName, "Core.slnx")))
                continue;

            return await File.ReadAllTextAsync(Path.Combine(
                directory.FullName,
                "src",
                "App",
                "LYBox.Launcher.Console",
                fileName));
        }

        throw new DirectoryNotFoundException("Could not locate the LY.Tool repository root.");
    }

    private sealed class FakePluginManagementService(PluginInfo plugin) : IPluginManagementService
    {
        public int GetInstalledPluginsCalls { get; private set; }
        public string? RequestedPluginId { get; private set; }
        public string? InstalledPackagePath { get; private set; }
        public string? UninstalledPluginId { get; private set; }

        public PluginInstallResult InstallResult { get; init; } = new()
        {
            Success = true,
            PluginInfo = plugin
        };

        public PluginUninstallResult UninstallResult { get; init; } =
            PluginUninstallResult.Succeeded(plugin.WithState(PluginState.PendingUninstall));

        public event EventHandler<PluginInfo>? PluginInstalled
        {
            add { }
            remove { }
        }

        public event EventHandler<PluginInfo>? PluginUninstalled
        {
            add { }
            remove { }
        }

        public event EventHandler<PluginInfo>? PluginUpgradeScheduled
        {
            add { }
            remove { }
        }

        public event EventHandler<PluginInfo>? PluginStateChanged
        {
            add { }
            remove { }
        }

        public IReadOnlyList<PluginInfo> GetInstalledPlugins()
        {
            GetInstalledPluginsCalls++;
            return [plugin];
        }

        public PluginInfo? GetPlugin(string pluginId)
        {
            RequestedPluginId = pluginId;
            return pluginId == plugin.PluginId ? plugin : null;
        }

        public bool IsReadOnly(string pluginId) => false;

        public bool CanUninstall(string pluginId) => true;

        public Task<PluginInstallResult> InstallFromFileAsync(
            string packageFilePath,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            InstalledPackagePath = packageFilePath;
            return Task.FromResult(InstallResult);
        }

        public Task<PluginUninstallResult> UninstallAsync(
            string pluginId,
            CancellationToken cancellationToken = default)
        {
            UninstalledPluginId = pluginId;
            return Task.FromResult(UninstallResult);
        }

        public Task<bool> CancelUpgradeAsync(
            string pluginId,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> EnablePluginAsync(
            string pluginId,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> DisablePluginAsync(
            string pluginId,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
