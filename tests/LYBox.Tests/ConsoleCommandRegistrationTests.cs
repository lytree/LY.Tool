using System.CommandLine;
using System.Text.Json;
using LYBox.Launcher.Console;
using LYBox.Plugin.Shared.CommandLine;
using LYBox.Plugin.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TUnit.Core;
using TUnit.Assertions;

namespace LYBox.Tests;

/// <summary>
/// 控制台启动器（LYBox.Launcher.Console）的单元测试，覆盖：
/// <list type="bullet">
/// <item>无参数时回退到 GUI 桌面启动（向后兼容）。</item>
/// <item>version 命令打印 Spectre 输出但不会触发桌面启动。</item>
/// <item><see cref="PluginCommandRegistry"/> 校验：插件 ID / 命令名归一化、唯一性、重复检测。</item>
/// </list>
/// </summary>
public class ConsoleCommandRegistrationTests
{
    [Test]
    public async Task ConsoleApplication_NoArguments_StartsDesktopForCompatibility()
    {
        var desktopStarted = false;
        var console = CreateConsole(out _);
        var application = new ConsoleApplication(
            console,
            _ => desktopStarted = true,
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        var exitCode = await application.RunAsync([]);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(desktopStarted).IsTrue();
    }

    [Test]
    public async Task ConsoleApplication_Version_WritesSpectreOutputWithoutStartingDesktop()
    {
        var desktopStarted = false;
        var console = CreateConsole(out var output);
        var application = new ConsoleApplication(
            console,
            _ => desktopStarted = true,
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        var exitCode = await application.RunAsync(["version"]);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(desktopStarted).IsFalse();
        await Assert.That(output.ToString()).Contains("LYBox Launcher");
    }

    [Test]
    public async Task ConsoleApplication_LightCommands_DoNotCreatePluginHost()
    {
        var hostCreations = 0;
        var desktopStarted = false;
        var console = CreateConsole(out _);
        var application = new ConsoleApplication(
            console,
            _ => desktopStarted = true,
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            (_, _, _) =>
            {
                hostCreations++;
                return Task.FromResult<IPluginCliHost>(new FakePluginCliHost(console));
            });

        await application.RunAsync(["version"]);
        await application.RunAsync(["--help"]);
        await application.RunAsync(["plugins", "list"]);
        await application.RunAsync(["gui"]);
        await application.RunAsync(["plugin", "--help"]);
        await application.RunAsync(["plugin", "run", "--help"]);

        await Assert.That(hostCreations).IsEqualTo(0);
        await Assert.That(desktopStarted).IsTrue();
    }

    [Test]
    public async Task ConsoleApplication_PluginRun_LoadsHostAndInvokesRegisteredCommand()
    {
        using var workspace = await PluginCatalogWorkspace.CreateAsync();
        var hostCreations = 0;
        var console = CreateConsole(out var output);
        var application = new ConsoleApplication(
            console,
            _ => { },
            workspace.PluginsDirectory,
            createPluginHost: (_, _, _) =>
            {
                hostCreations++;
                return Task.FromResult<IPluginCliHost>(new FakePluginCliHost(console));
            });

        var exitCode = await application.RunAsync(["plugin", "run", "sample.plugin", "echo"]);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(hostCreations).IsEqualTo(1);
        await Assert.That(output.ToString()).Contains("fake plugin command");
    }

    [Test]
    public async Task ConsoleApplication_LegacyPluginSyntax_RemainsSupported()
    {
        using var workspace = await PluginCatalogWorkspace.CreateAsync();
        var console = CreateConsole(out var output);
        var application = new ConsoleApplication(
            console,
            _ => { },
            workspace.PluginsDirectory,
            createPluginHost: (_, _, _) =>
                Task.FromResult<IPluginCliHost>(new FakePluginCliHost(console)));

        var exitCode = await application.RunAsync(["plugin", "sample", "echo"]);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output.ToString()).Contains("fake plugin command");
    }

    [Test]
    public async Task ConsoleApplication_PluginHostCreationFailure_IsCaught()
    {
        using var workspace = await PluginCatalogWorkspace.CreateAsync();
        var console = CreateConsole(out var output);
        var application = new ConsoleApplication(
            console,
            _ => { },
            workspace.PluginsDirectory,
            createPluginHost: (_, _, _) =>
                throw new InvalidOperationException("host creation failed"));

        var exitCode = await application.RunAsync(["plugin", "sample", "echo"]);

        await Assert.That(exitCode).IsEqualTo(PluginCliExitCodes.HostFailure);
        await Assert.That(output.ToString()).Contains("host creation failed");
    }

    [Test]
    public async Task PluginCliHost_ConfigureServices_RegistersAnsiConsole()
    {
        var console = CreateConsole(out _);
        var services = new ServiceCollection();

        PluginCliHost.ConfigureServices(services, console);
        await using var provider = services.BuildServiceProvider();

        await Assert.That(provider.GetRequiredService<IAnsiConsole>()).IsSameReferenceAs(console);
    }

    [Test]
    public async Task PluginCommandRegistry_NormalizesCommandName_LowercasesAndTrims()
    {
        var normalized = PluginCommandRegistry.NormalizeCommandName("  Sample-CLI  ");

        await Assert.That(normalized).IsEqualTo("sample-cli");
    }

    [Test]
    public async Task PluginCommandRegistry_NormalizeCommandName_RejectsInvalidCharacters()
    {
        await Assert.That(() => PluginCommandRegistry.NormalizeCommandName("sample cli"))
            .Throws<ArgumentException>();
        await Assert.That(() => PluginCommandRegistry.NormalizeCommandName("sample.cli"))
            .Throws<ArgumentException>();
        await Assert.That(() => PluginCommandRegistry.NormalizeCommandName("sample/cli"))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task PluginCommandRegistry_NormalizeCommandName_RejectsEmptyOrWhitespace()
    {
        await Assert.That(() => PluginCommandRegistry.NormalizeCommandName(""))
            .Throws<ArgumentException>();
        await Assert.That(() => PluginCommandRegistry.NormalizeCommandName("   "))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task PluginCommandRegistry_UsesExplicitServiceRegistration()
    {
        var console = CreateConsole(out var output);
        var services = new ServiceCollection();
        services.AddSingleton<IPluginCommandRegistrar>(new RecordingCommandRegistrar());
        await using var provider = services.BuildServiceProvider();

        var root = new RootCommand();
        var plugin = new Command("plugin");
        root.Subcommands.Add(plugin);

        var count = PluginCommandRegistry.RegisterCommands(plugin, provider, console);
        var exitCode = root.Parse(["plugin", "sample", "echo"]).Invoke();

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output.ToString()).Contains("registered command");
    }

    [Test]
    public async Task PluginCommandRegistry_DetectsDuplicateCommandNames()
    {
        var console = CreateConsole(out _);
        var services = new ServiceCollection();
        services.AddSingleton<IPluginCommandRegistrar>(new RecordingCommandRegistrar("plugin.a", "sample"));
        services.AddSingleton<IPluginCommandRegistrar>(new RecordingCommandRegistrar("plugin.b", "Sample"));
        await using var provider = services.BuildServiceProvider();

        var plugin = new Command("plugin");
        await Assert.That(() => PluginCommandRegistry.RegisterCommands(plugin, provider, console))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task PluginCommandRegistry_DetectsDuplicatePluginIds()
    {
        var console = CreateConsole(out _);
        var services = new ServiceCollection();
        services.AddSingleton<IPluginCommandRegistrar>(new RecordingCommandRegistrar("plugin.a", "first"));
        services.AddSingleton<IPluginCommandRegistrar>(new RecordingCommandRegistrar("plugin.a", "second"));
        await using var provider = services.BuildServiceProvider();

        var plugin = new Command("plugin");
        await Assert.That(() => PluginCommandRegistry.RegisterCommands(plugin, provider, console))
            .Throws<InvalidOperationException>();
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

    private sealed class RecordingCommandRegistrar : IPluginCommandRegistrar
    {
        private readonly string _pluginId;
        private readonly string _commandName;

        public RecordingCommandRegistrar() : this("sample.plugin", "sample") { }

        public RecordingCommandRegistrar(string pluginId, string commandName)
        {
            _pluginId = pluginId;
            _commandName = commandName;
        }

        public string PluginId => _pluginId;

        public string CommandName => _commandName;

        public string Description => "Sample test commands.";

        public void RegisterCommands(PluginCommandRegistrationContext context)
        {
            var echo = new Command("echo");
            echo.SetAction(_ =>
            {
                context.Console.WriteLine("registered command");
                return 0;
            });
            context.Command.Subcommands.Add(echo);
        }
    }

    private sealed class FakePluginCliHost : IPluginCliHost
    {
        private readonly IAnsiConsole _console;

        public FakePluginCliHost(IAnsiConsole console)
        {
            _console = console;
        }

        public int RegisterCommands(
            Command pluginCommand,
            Command runCommand,
            PluginInvocationRoute route)
        {
            var echo = new Command("echo");
            echo.SetAction(_ =>
            {
                _console.WriteLine("fake plugin command");
                return 0;
            });

            if (route == PluginInvocationRoute.Explicit)
            {
                runCommand.Subcommands.Add(echo);
            }
            else
            {
                var sample = new Command("sample");
                sample.Subcommands.Add(echo);
                pluginCommand.Subcommands.Add(sample);
            }

            return 1;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class PluginCatalogWorkspace : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly string _rootDirectory;

        private PluginCatalogWorkspace(string rootDirectory)
        {
            _rootDirectory = rootDirectory;
            PluginsDirectory = Path.Combine(rootDirectory, "plugins");
        }

        public string PluginsDirectory { get; }

        public static async Task<PluginCatalogWorkspace> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), "LYBox.Tests", Guid.NewGuid().ToString("N"));
            var workspace = new PluginCatalogWorkspace(root);
            var pluginDirectory = Path.Combine(workspace.PluginsDirectory, "sample.plugin");
            Directory.CreateDirectory(pluginDirectory);
            const string assemblyName = "Sample.Plugin.dll";
            File.Copy(
                typeof(ConsoleCommandRegistrationTests).Assembly.Location,
                Path.Combine(pluginDirectory, assemblyName));

            await WriteJsonAsync(
                Path.Combine(pluginDirectory, "plugin.json"),
                new PluginManifest
                {
                    PluginId = "sample.plugin",
                    Name = "Sample Plugin",
                    Version = "1.0.0",
                    Assembly = assemblyName,
                    State = nameof(PluginState.Installed)
                });
            await WriteJsonAsync(
                Path.Combine(pluginDirectory, "plugin.cli.json"),
                new PluginCliIndex
                {
                    PluginId = "sample.plugin",
                    PluginVersion = "1.0.0",
                    Alias = "sample",
                    Description = "Sample plugin commands.",
                    RuntimeProfile = "selected",
                    OutputModes = ["text", "json"],
                    Commands = [new PluginCliCommandDefinition { Name = "echo", Description = "Echo." }]
                });
            return workspace;
        }

        public void Dispose()
        {
            if (Directory.Exists(_rootDirectory))
                Directory.Delete(_rootDirectory, recursive: true);
        }

        private static async Task WriteJsonAsync<T>(string path, T value)
        {
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, value, JsonOptions);
        }
    }
}
