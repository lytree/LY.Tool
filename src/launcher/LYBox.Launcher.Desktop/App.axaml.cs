using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Models;
using LYBox.Plugin.Shared.Services;
using LYBox.Layout.Core.Data;
using LYBox.Layout.Core.Services;
using LYBox.Layout.Ursa.Services;
using LYBox.Layout.Ursa.ViewModels;
using LYBox.Layout.Ursa.Views;
using LYBox.Layout.Fluent.Services;
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
    private bool _isShuttingDown;

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
        // 根据布局模式注册不同的宿主层服务
        // Ursa: NavigationService / MenuConfigurationService / LocalizationService（依赖 Ursa 的 ViewModel/Page/Theme）
        // Fluent: NavigationService / MenuConfigurationService（不依赖 Ursa，与 Ursa 布局独立）
        var layoutMode = LYBox.Launcher.Desktop.Program.LayoutMode;
        if (string.Equals(layoutMode, "fluent", StringComparison.OrdinalIgnoreCase))
        {
            services.AddFluentServices();
        }
        else
        {
            services.AddUrsaServices();
        }

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
        // 使用完全限定名避免 Ursa/Fluent 两个 NavigationService 类型之间的歧义
        if (string.Equals(layoutMode, "fluent", StringComparison.OrdinalIgnoreCase))
        {
            if (ServiceProvider.GetRequiredService<INavigationService>() is LYBox.Layout.Fluent.Services.NavigationService fluentNav)
                fluentNav.AttachPluginLoader(pluginLoader);
        }
        else
        {
            if (ServiceProvider.GetRequiredService<INavigationService>() is LYBox.Layout.Ursa.Services.NavigationService ursaNav)
                ursaNav.AttachPluginLoader(pluginLoader);
        }

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

            var layoutMode = LYBox.Launcher.Desktop.Program.LayoutMode;

            if (string.Equals(layoutMode, "fluent", StringComparison.OrdinalIgnoreCase))
            {
                // --layout=fluent：使用 Avalonia-Fluent-UI Gallery 布局
                var config = LYBox.Layout.Fluent.Services.ConfigService.LoadConfig();
                var fluentVm = new LYBox.Layout.Fluent.ViewModels.MainWindowViewModel(config);
                // 接入插件系统：注入导航和菜单服务
                var navService = ServiceLocator.GetService<INavigationService>();
                var menuService = ServiceLocator.GetService<IMenuConfigurationService>();
                fluentVm.InitializePluginSystem(navService, menuService);

                desktop.MainWindow = new LYBox.Layout.Fluent.Views.MainWindow
                {
                    DataContext = fluentVm
                };
            }
            else
            {
                // 默认 --layout=ursa：使用 Ursa 布局
                if (LYBox.Launcher.Desktop.Program.NoSplash)
                {
                    // --no-splash：跳过闪屏，直接显示主窗口
                    desktop.MainWindow = new MainWindow()
                    {
                        DataContext = new MainWindowViewModel()
                    };
                }
                else
                {
                    desktop.MainWindow = new MvvmSplashWindow()
                    {
                        DataContext = new SplashViewModel()
                    };
                }
            }

            // 退出时检测是否有正在运行的任务
            desktop.ShutdownRequested += OnShutdownRequested;

            InitializeTrayIcon();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 创建系统托盘图标（跨平台，使用 Avalonia 12.1 内置 TrayIcon）。
    /// 图标加载自 LYBox.UI 的 AvaloniaResource：avares://LYBox.UI/Assets/lybox.ico。
    /// 菜单项命令挂接到 ApplicationViewModel 的 ShowMainWindow/ExitApplication 命令。
    /// Avalonia 在应用退出时自动 Dispose 托盘图标，无需手动清理。
    /// </summary>
    private void InitializeTrayIcon()
    {
        var loc = ServiceLocator.TryGetService<ILocalizationService>(out var locSvc) ? locSvc : null;
        var tooltip = loc?.GetString("TRAY_TOOLTIP", "LYBox") ?? "LYBox";
        var showText = loc?.GetString("TRAY_SHOW_WINDOW", "Show Window") ?? "Show Window";
        var exitText = loc?.GetString("TRAY_EXIT", "Exit") ?? "Exit";

        var vm = DataContext as ApplicationViewModel;

        WindowIcon? trayWindowIcon = null;
        try
        {
            var iconUri = new Uri("avares://LYBox.UI/Assets/lybox.ico");
            using var stream = AssetLoader.Open(iconUri);
            trayWindowIcon = new WindowIcon(stream);
        }
        catch (Exception ex)
        {
            // 图标加载失败不阻塞托盘创建（ToolTipText 仍可见）
            Console.WriteLine($"Failed to load tray icon: {ex.Message}");
        }

        var trayIcon = new TrayIcon
        {
            Icon = trayWindowIcon,
            ToolTipText = tooltip,
            IsVisible = true,
            Menu = new NativeMenu
            {
                new NativeMenuItem(showText) { Command = vm?.ShowMainWindowCommand },
                new NativeMenuItemSeparator(),
                new NativeMenuItem(exitText) { Command = vm?.ExitApplicationCommand }
            }
        };

        var icons = new TrayIcons { trayIcon };
        TrayIcon.SetIcons(this, icons);
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_isShuttingDown)
            return;

        _isShuttingDown = true;
        e.Cancel = true;

        if (ServiceLocator.TryGetService<ITaskRegistry>(out var registry) && registry.HasRunningTasks)
        {
            var tasks = registry.GetRunningTasks();
            var taskNames = string.Join(", ", tasks.Select(t => t.TaskName));
            var logger = ServiceProvider?.GetRequiredService<ILogger<App>>();
            logger?.LogWarning("应用退出时仍有正在运行的任务: {Tasks}", taskNames);
        }

        // 修复：异步清理 + 超时兜底，避免原生资源释放（TdLib/ZLogger）阻塞导致进程无法退出
        // 先取消关闭请求，然后在线程池线程上执行清理，完成后调用 Environment.Exit 强制退出
        _ = Task.Run(async () =>
        {
            var cleanupTask = PerformCleanupAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            var completed = await Task.WhenAny(cleanupTask, timeoutTask);

            if (completed == timeoutTask)
            {
                Console.Error.WriteLine("[Shutdown] Cleanup timed out after 10s, forcing exit.");
            }

            Environment.Exit(0);
        });
    }

    private async Task PerformCleanupAsync()
    {
        try
        {
            if (_pluginLoader is not null)
            {
                await _pluginLoader.ShutdownAllPluginsAsync();
            }
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

        // 取消订阅全局异常处理，避免在 ServiceProvider Dispose 后日志器失效导致异常处理再抛异常
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
        Avalonia.Threading.Dispatcher.UIThread.UnhandledException -= OnUIThreadUnhandledException;
    }
}
