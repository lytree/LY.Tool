using LYBox.Layout.Core.Services;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.CommandLine;
using LYBox.Plugin.Shared.Services;
using LYBox.Launcher.Desktop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using SpectreMarkup = Spectre.Console.Markup;

namespace LYBox.Launcher.Console;

internal delegate Task<IPluginCliHost> PluginCliHostFactory(
    IAnsiConsole console,
    string? pluginsDirectory,
    CancellationToken cancellationToken);

internal interface IPluginCliHost : IAsyncDisposable
{
    int RegisterCommands(System.CommandLine.Command pluginCommand);
}

/// <summary>
/// 插件 CLI 宿主：构建 ServiceProvider、加载已安装插件、把 <see cref="IPluginCommandRegistrar"/>
/// 注册到根 <c>plugin</c> 子命令下，最后负责释放插件与服务。
/// <para>
/// 设计要点：
/// </para>
/// <list type="bullet">
/// <item>完整走 <see cref="PluginLoader"/> 两阶段加载（Discover → Initialize → Register），复用宿主 GUI 启动器
///       的全部 DI 注册（含 EF Core / 本地化 / 设置 / 任务注册表）。</item>
/// <item>插件通过 <c>IPluginCommandRegistrar</c> 注册的命令在 <c>plugin &lt;name&gt; ...</c> 下暴露，
///       解析、help、错误处理由 System.CommandLine 统一接管。</item>
/// <item><see cref="DisposeAsync"/> 按 LIFO 顺序 <c>ShutdownAsync()</c> 插件并 Dispose ServiceProvider，
///       防止 TdLib / Kestrel 等原生资源泄漏。</item>
/// </list>
/// </summary>
internal sealed class PluginCliHost : IPluginCliHost
{
    private readonly IAnsiConsole _console;
    private readonly PluginLoader _pluginLoader;
    private readonly ServiceProvider _serviceProvider;

    private PluginCliHost(
        IAnsiConsole console,
        PluginLoader pluginLoader,
        ServiceProvider serviceProvider)
    {
        _console = console;
        _pluginLoader = pluginLoader;
        _serviceProvider = serviceProvider;
    }

    public static async Task<PluginCliHost> CreateAsync(
        IAnsiConsole console,
        string? pluginsDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(console);

        var services = new ServiceCollection();
        ConfigureServices(services, console);

        var loader = new PluginLoader(pluginsDirectory);
        ServiceProvider? provider = null;
        try
        {
            // LYBox.PluginLoader 在构造函数内同步完成 manifests 扫描与 pending upgrade / uninstall 处理，
            // 无单独的 InitializeAsync 阶段（与 windit 异步初始化契约不同）。
            // 阶段1：发现并加载插件程序集
            await loader.DiscoverAllPluginAssembliesAsync().ConfigureAwait(false);
            // 阶段2：插件向 IServiceCollection 注册自己的服务（包含 IPluginCommandRegistrar）
            await loader.InitializeAllPluginsAsync(services).ConfigureAwait(false);

            // 把提前实例化的 loader 重新挂到 DI（与 GUI 启动器 App.InitializeCoreAsync 一致）
            services.RemoveAll<PluginLoader>();
            services.AddSingleton<PluginLoader>(loader);
            services.RemoveAll<IPluginLoader>();
            services.AddSingleton<IPluginLoader>(loader);

            provider = services.BuildServiceProvider();
            ServiceLocator.Initialize(provider);

            // 把 ILogger 注入到 PluginLoader（构造期使用 NullLogger）
            PluginLoader.SetLogger(provider.GetRequiredService<ILogger<PluginLoader>>());

            // 数据库迁移（与 App.InitializeCoreAsync 对齐）
            await provider.GetRequiredService<DatabaseMigrationService>()
                .MigrateAsync(cancellationToken).ConfigureAwait(false);

            // 设置默认值（如有 SettingsService 实现）—— 与 App.InitializeCoreAsync 中的
            // settingsService.InitializeDefaults() 对齐，settings 由插件 RegisterAsync 阶段动态注册。
            if (provider.GetService<ISettingsService>() is SettingsService settings)
                settings.InitializeDefaults();

            // 阶段3：插件注册运行时依赖（多语言资源、设置项等）
            await loader.RegisterAllPluginsAsync(provider).ConfigureAwait(false);

            return new PluginCliHost(console, loader, provider);
        }
        catch
        {
            if (provider is not null)
                await provider.DisposeAsync().ConfigureAwait(false);
            loader.Dispose();
            throw;
        }
    }

    internal static void ConfigureServices(
        IServiceCollection services,
        IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(console);

        // 复用宿主 GUI 启动器的核心 DI，并让插件 CLI 服务获得同一输出端。
        services.AddAvaloniaServices();
        services.AddSingleton<IAnsiConsole>(console);
    }

    /// <summary>
    /// 把当前 ServiceProvider 中所有 <see cref="IPluginCommandRegistrar"/> 挂到根 <c>plugin</c> 子命令下。
    /// 返回注册的子命令数。
    /// </summary>
    public int RegisterCommands(System.CommandLine.Command pluginCommand)
    {
        return PluginCommandRegistry.RegisterCommands(pluginCommand, _serviceProvider, _console);
    }

    public async ValueTask DisposeAsync()
    {
        // 按 LIFO 顺序 ShutdownAsync 插件，单个插件异常不影响其他插件
        var plugins = _pluginLoader.GetInstalledPlugins()
            .Select(plugin => _pluginLoader.GetLoadedPlugin(plugin.PluginId))
            .OfType<IPlugin>()
            .Reverse()
            .ToArray();

        foreach (var plugin in plugins)
        {
            try
            {
                await plugin.ShutdownAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _console.MarkupLine($"[yellow]插件退出告警：[/] {SpectreMarkup.Escape(exception.Message)}");
            }
        }

        _pluginLoader.Dispose();
        await _serviceProvider.DisposeAsync().ConfigureAwait(false);
    }
}
