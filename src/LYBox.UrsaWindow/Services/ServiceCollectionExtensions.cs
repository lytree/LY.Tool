using LYBox.Plugin.Shared.Services;
using LYBox.UrsaWindow.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Formatters;
using ZLogger.Providers;

namespace LYBox.UrsaWindow.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAvaloniaServices(this IServiceCollection services)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logPath);

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddFilter("Microsoft", LogLevel.Warning);
            builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

            // 控制台与文件使用相同的纯文本前缀格式：
            //   [2026-07-10 15:39:43.123] [INFO] [LYBox.UrsaWindow.Services.NavigationService] 消息内容
            // 异常信息由 PlainTextZLoggerFormatter 默认行为追加到消息末尾（换行 + 异常类型:消息 + 堆栈）。
            builder.AddZLoggerConsole(options =>
            {
                ConfigurePlainTextFormatter(options);
            });
            builder.AddZLoggerRollingFile(options =>
            {
                options.FilePathSelector = (dt, seq) =>
                    Path.Combine(logPath, $"app-{dt:yyyy-MM-dd}_{seq:000}.log");
                options.RollingInterval = RollingInterval.Day;
                options.RollingSizeKB = 10240; // 10MB
                ConfigurePlainTextFormatter(options);
            });
        });

        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IMenuConfigurationService, MenuConfigurationService>();

        // PluginLoader 由 App.Initialize() 提前实例化（阶段1/2需要 DI 尚未构建时使用），
        // 随后通过 services.AddSingleton(pluginLoader) 注入，此处不再注册以避免产生未使用的孤立实例。

        services.AddSingleton<IPluginInstallationManager, PluginInstallationManager>();

        services.AddDbContextFactory<AppDbContext>(options =>
        {
            var dbPath = Path.Combine(AppContext.BaseDirectory, "appdata.db");
            options.UseSqlite($"Data Source={dbPath}");
        });

        services.AddSingleton<ISettingsService, SettingsService>();

        services.AddSingleton<IWindowInfoService, WindowInfoService>();

        services.AddLocalization();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<ITaskRegistry, TaskRegistry>();

        return services;
    }

    /// <summary>
    /// 配置纯文本日志格式器，统一控制台与文件的输出格式。
    /// 输出示例：[2026-07-10 15:39:43.123] [Information] [LYBox.UrsaWindow.Services.NavigationService] 消息内容
    /// 异常信息自动追加到消息末尾（换行 + 异常类型:消息 + 堆栈 + 内部异常链）。
    /// </summary>
    private static void ConfigurePlainTextFormatter(ZLoggerOptions options)
    {
        options.IncludeScopes = true;
        options.UsePlainTextFormatter(formatter =>
        {
            formatter.SetPrefixFormatter(
                $"[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}] [{2}] ",
                static (in MessageTemplate template, in LogInfo info) =>
                {
                    template.Format(
                        info.Timestamp.Utc,
                        info.LogLevel,
                        info.Category.Name
                    );
                }
            );
        });
    }
}
