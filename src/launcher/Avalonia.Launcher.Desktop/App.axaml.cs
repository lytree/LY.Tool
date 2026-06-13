using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Models;
using Avalonia.Plugin.Shared.Services;
using Avalonia.UI.Data;
using Avalonia.UI.Services;
using Avalonia.UI.ViewModels;
using Avalonia.UI.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Avalonia.Launcher.Desktop;

public partial class App : Application
{
    public static IServiceProvider? ServiceProvider { get; private set; }

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
        e.Handled = true;
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
        pluginLoader.DiscoverAllPluginAssembliesAsync().GetAwaiter().GetResult();

        // 阶段2：调用插件 InitializeAsync(IServiceCollection)，注册 DI 服务
        pluginLoader.InitializeAllPluginsAsync(services).GetAwaiter().GetResult();

        services.AddSingleton<IPluginLoader>(sp =>
        {
            var navigationService = sp.GetRequiredService<INavigationService>() as NavigationService;
            navigationService?.AttachPluginLoader(pluginLoader);
            return pluginLoader;
        });

        ServiceProvider = services.BuildServiceProvider();
        ServiceLocator.Initialize(ServiceProvider);

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
            var culture = !string.IsNullOrEmpty(savedLocale)
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

        foreach (var pluginInfo in pluginLoader.GetInstalledPlugins())
        {
            if (pluginInfo.State != PluginState.Loaded && pluginInfo.State != PluginState.Registered)
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
                Console.WriteLine($"Error registering plugin {pluginInfo.Name}: {ex.Message}");
            }
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 全局异常处理：UI 线程未处理异常
            Avalonia.Threading.Dispatcher.UIThread.UnhandledException += OnUIThreadUnhandledException;

            desktop.MainWindow = new MvvmSplashWindow()
            {
                DataContext = new SplashViewModel()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            var navigationService = ServiceProvider?.GetRequiredService<INavigationService>();
            var menuConfigurationService = ServiceProvider?.GetRequiredService<IMenuConfigurationService>();
            singleView.MainView = new SingleView()
            {
                DataContext = new MainViewViewModel(navigationService!, menuConfigurationService!),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
