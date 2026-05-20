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
        services.AddSingleton<IPluginLoader>(sp => sp.GetRequiredService<PluginLoader>());
        services.AddSingleton<IPluginInstallationManager, PluginInstallationManager>();

        services.AddDbContextFactory<AppDbContext>(options =>
        {
            var dbPath = Path.Combine(AppContext.BaseDirectory, "appdata.db");
            options.UseSqlite($"Data Source={dbPath}");
        });

        services.AddSingleton<ISettingsService, SettingsService>();

        services.AddLocalization();
        services.AddSingleton<ILocalizationService, LocalizationService>();

        return services;
    }
}
