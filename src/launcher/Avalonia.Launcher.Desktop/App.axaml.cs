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

namespace Avalonia.Launcher.Desktop;

public partial class App : Application
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    public App()
    {
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
    this.AttachDeveloperTools();
#endif
        var services = new ServiceCollection();
        services.AddAvaloniaServices();
        ServiceProvider = services.BuildServiceProvider();

        ServiceLocator.Initialize(ServiceProvider);

        InitializeLocalization();

        InitializeDatabase();

        LoadPlugins();

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
                : new System.Globalization.CultureInfo("en-US");
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

    private void LoadPlugins()
    {
        var navigationService = ServiceProvider?.GetRequiredService<INavigationService>();
        var menuConfigurationService = ServiceProvider?.GetRequiredService<IMenuConfigurationService>();
        var pluginLoader = ServiceProvider?.GetRequiredService<IPluginLoader>();

        if (navigationService == null || menuConfigurationService == null || pluginLoader == null)
            return;

        pluginLoader.LoadAllPlugins();

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
                Console.WriteLine($"Error registering plugin {pluginInfo.Name}: {ex.Message}");
            }
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
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
