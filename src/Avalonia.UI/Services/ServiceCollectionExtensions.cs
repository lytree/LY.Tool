using Avalonia.Plugin.Shared.Services;
using Avalonia.UI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Avalonia.UI.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAvaloniaServices(this IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IMenuConfigurationService, MenuConfigurationService>();

        services.AddSingleton<PluginLoader>();
        services.AddSingleton<IPluginLoader>(sp =>
        {
            var loader = sp.GetRequiredService<PluginLoader>();
            var navigationService = sp.GetRequiredService<INavigationService>() as NavigationService;
            navigationService?.AttachPluginLoader(loader);
            return loader;
        });
        services.AddSingleton<IPluginInstallationManager, PluginInstallationManager>();

        services.AddDbContextFactory<AppDbContext>(options =>
        {
            var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AvaloniaTemplate");
            Directory.CreateDirectory(appDataDir);
            var dbPath = Path.Combine(appDataDir, "appdata.db");
            options.UseSqlite($"Data Source={dbPath}");
        });

        services.AddSingleton<ISettingsService, SettingsService>();

        services.AddSingleton<IWindowInfoService, WindowInfoService>();

        services.AddLocalization();
        services.AddSingleton<ILocalizationService, LocalizationService>();

        return services;
    }
}
