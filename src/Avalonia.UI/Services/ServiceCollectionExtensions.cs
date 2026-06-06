using Avalonia.Plugin.Shared.Services;
using Avalonia.UI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;

namespace Avalonia.UI.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAvaloniaServices(this IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IMenuConfigurationService, MenuConfigurationService>();

        // PluginLoader 由 App.axaml.cs 手动创建并注册，此处不再注册

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

        // ZLogger 日志系统：按天滚动文件 + 控制台输出
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Debug);

            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AvaloniaTemplate", "logs");
            Directory.CreateDirectory(logDir);

            // 按天滚动文件：文件名格式 logs/app_2026-06-06_0.log
            logging.AddZLoggerRollingFile(
                filePathSelector: (dt, seq) =>
                {
                    var date = dt.ToLocalTime().ToString("yyyy-MM-dd");
                    return Path.Combine(logDir, $"app_{date}_{seq:000}.log");
                },
                rollInterval: RollingInterval.Day,
                rollSizeKB: 10240); // 10MB per file

            // 控制台输出
            logging.AddZLoggerConsole();
        });

        return services;
    }
}
