using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Models;
using LYBox.Plugin.Shared.Services;
using LYBox.UrsaWindow.Data;
using LYBox.UrsaWindow.Services;
using LYBox.UrsaWindow.ViewModels;
using LYBox.UrsaWindow.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace LYBox.Launcher.Desktop;

public partial class App : Application
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    // 保存 pluginLoader 引用用于退出时 ShutdownAsync 与 Dispose（Dispose 内部会再次调用 ShutdownAsync，幂等安全）
    private PluginLoader? _pluginLoader;

    public App()
    {
        // 全局异常处理：后台线程未观察到的异常
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogGlobalException("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogGlobalException("UnhandledException", ex);
    }

    private static void LogGlobalException(string source, Exception ex)
    {
        try
        {
            var logger = ServiceProvider?.GetRequiredService<ILogger<App>>();
            logger?.LogError(ex, "[全局异常] {Source}: {Message}", source, ex.Message);
        }
        catch
        {
            Console.Error.WriteLine($"[全局异常] {source}: {ex}");
        }
    }

    private static void OnUIThreadUnhandledException(object? sender, Avalonia.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogGlobalException("UIThreadUnhandledException", e.Exception);
        Console.Error.WriteLine($"[UIThreadUnhandledException] {e.Exception}");
#if DEBUG
        // DEBUG 模式下不吞异常，让问题暴露
        e.Handled = false;
#else
        e.Handled = true;
#endif
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
        var services = new ServiceCollection();
        services.AddAvaloniaServices();

        // 阶段1：发现所有插件程序集，创建 IPlugin 实例
        var pluginLoader = new PluginLoader();
        _pluginLoader = pluginLoader;
        pluginLoader.DiscoverAllPluginAssembliesAsync().GetAwaiter().GetResult();

        // 阶段2：调用插件 InitializeAsync(IServiceCollection)，注册 DI 服务
        pluginLoader.InitializeAllPluginsAsync(services).GetAwaiter().GetResult();

        // 统一注入：将提前实例化的 pluginLoader 注册到 DI（避免双重注册产生孤立实例）
        services.AddSingleton<PluginLoader>(pluginLoader);
        services.AddSingleton<IPluginLoader>(pluginLoader);

        ServiceProvider = services.BuildServiceProvider();
        ServiceLocator.Initialize(ServiceProvider);

        // 注入 logger 到 PluginLoader（构造期使用 NullLogger）
        PluginLoader.SetLogger(ServiceProvider.GetRequiredService<ILogger<PluginLoader>>());

        // 显式连接 NavigationService 与 PluginLoader（原嵌入在 DI 工厂中的副作用，移出以保证时序确定）
        var navigationService = ServiceProvider.GetRequiredService<INavigationService>() as NavigationService;
        navigationService?.AttachPluginLoader(pluginLoader);

        // 记录应用启动日志
        var logger = ServiceProvider.GetRequiredService<ILogger<App>>();
        logger.ZLogInformation($"AvaloniaTemplate 应用启动");

        InitializeDatabase();
        InitializeLocalization();

        // 阶段3：调用插件 RegisterAsync(IServiceProvider)，执行多语言注册等
        pluginLoader.RegisterAllPluginsAsync(ServiceProvider).GetAwaiter().GetResult();
        RegisterPluginNavigationAndMenus(pluginLoader);

        DataContext = new ApplicationViewModel();
    }

    private void InitializeLocalization()
    {
        if (ServiceLocator.TryGetService<ILocalizationService>(out var loc) && loc is not null)
        {
            var settingsService = ServiceProvider?.GetRequiredService<ISettingsService>();
            var savedLocale = settingsService?.GetValue("App.Locale");
            var culture = !string.IsNullOrEmpty(savedLocale) && !"Default".Equals(savedLocale)
                ? new System.Globalization.CultureInfo(savedLocale)
                : System.Globalization.CultureInfo.CurrentUICulture;
            loc.SetCulture(culture);
        }
    }

    private void InitializeDatabase()
    {
        var dbFactory = ServiceProvider?.GetRequiredService<IDbContextFactory<AppDbContext>>();
        if (dbFactory == null) return;

        using var db = dbFactory.CreateDbContext();
        db.Database.EnsureCreated();

        var settingsService = ServiceProvider?.GetRequiredService<ISettingsService>() as SettingsService;
        settingsService?.InitializeDefaults();
    }

    private void RegisterPluginNavigationAndMenus(IPluginLoader pluginLoader)
    {
        var navigationService = ServiceProvider?.GetRequiredService<INavigationService>();
        var menuConfigurationService = ServiceProvider?.GetRequiredService<IMenuConfigurationService>();

        if (navigationService == null || menuConfigurationService == null)
            return;

        // 修复 #12：原 catch 仅 Console.WriteLine，未持久化插件错误状态，UI 上仍显示为已加载，
        // 用户无法感知插件故障。改为：失败时调用 MarkPluginError 持久化状态，并记录结构化日志。
        var logger = ServiceProvider?.GetRequiredService<ILogger<App>>();

        foreach (var pluginInfo in pluginLoader.GetInstalledPlugins())
        {
            if (pluginInfo.State != PluginState.Loaded)
                continue;

            try
            {
                var plugin = pluginLoader.GetLoadedPlugin(pluginInfo.PluginId);
                if (plugin == null) continue;

                var navigationItems = plugin.GetNavigationItems();
                navigationService.RegisterNavigations(navigationItems);

                var menuItems = plugin.GetMenuItems();
                menuConfigurationService.RegisterMenuItems(menuItems);

                ViewLocator.RegisterPlugin(plugin);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "注册插件 {PluginId} 导航/菜单失败", pluginInfo.PluginId);
                pluginLoader.MarkPluginError(pluginInfo.PluginId, $"Registration failed: {ex.Message}");
            }
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 全局异常处理：UI 线程未处理异常
            Avalonia.Threading.Dispatcher.UIThread.UnhandledException += OnUIThreadUnhandledException;

            if (LYBox.Launcher.Desktop.Program.NoSplash)
            {
                // --no-splash：跳过闪屏，直接显示主窗口
                var navigationService = ServiceLocator.GetService<INavigationService>();
                var menuConfigurationService = ServiceLocator.GetService<IMenuConfigurationService>();
                desktop.MainWindow = new MainWindow()
                {
                    DataContext = new MainViewViewModel(navigationService!, menuConfigurationService!)
                };
            }
            else
            {
                desktop.MainWindow = new MvvmSplashWindow()
                {
                    DataContext = new SplashViewModel()
                };
            }

            // 退出时检测是否有正在运行的任务
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (ServiceLocator.TryGetService<ITaskRegistry>(out var registry) && registry.HasRunningTasks)
        {
            var tasks = registry.GetRunningTasks();
            var taskNames = string.Join(", ", tasks.Select(t => t.TaskName));
            var logger = ServiceProvider?.GetRequiredService<ILogger<App>>();
            logger?.LogWarning("应用退出时仍有正在运行的任务: {Tasks}", taskNames);
        }

        // 修复 #1+#2+#4：完整退出流程——
        //   1) 优雅关闭插件（IPlugin.ShutdownAsync），释放插件持有的原生资源（如 TdLib 客户端）
        //   2) Dispose ServiceProvider，触发所有 Singleton 的 Dispose（IDbContextFactory、ZLogger 等）
        //   3) Dispose PluginLoader（内部会再次 ShutdownAsync，幂等；并 ALC.Unload）
        // ShutdownRequestedEventArgs 不支持 async，使用 Task.Run 避免在 UI 线程上 sync-over-async 死锁
        try
        {
            Task.Run(async () =>
            {
                if (_pluginLoader is not null)
                {
                    await _pluginLoader.ShutdownAllPluginsAsync();
                }
            }).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ShutdownAllPluginsAsync failed on exit: {ex.Message}");
        }

        try
        {
            (ServiceProvider as IDisposable)?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ServiceProvider.Dispose failed on exit: {ex.Message}");
        }

        try
        {
            _pluginLoader?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PluginLoader.Dispose failed on exit: {ex.Message}");
        }

        // 修复 #14：取消订阅全局异常处理，避免在 ServiceProvider Dispose 后日志器失效导致异常处理再抛异常
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
        Avalonia.Threading.Dispatcher.UIThread.UnhandledException -= OnUIThreadUnhandledException;
    }
}
