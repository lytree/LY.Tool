using LYBox.Plugin.Shared.Services;
using LYBox.Layout.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LYBox.Layout.Ursa.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Ursa 宿主层特有的 DI 服务：导航、菜单配置、本地化。
    /// 这些实现依赖 Ursa 的 ViewModel / Page / Theme 类型，因此必须留在 Ursa 程序集，
    /// 不能下沉到 LYBox.Layout.Core（否则 Core 反向依赖 Ursa，形成循环引用）。
    /// 调用方应先调用 LYBox.Layout.Core.Services.ServiceCollectionExtensions.AddAvaloniaServices()
    /// 注册核心服务（含 ISettingsService、IPluginLoader、IPluginInstallationManager 等），
    /// 再调用本方法补充 Ursa 层服务。
    /// </summary>
    public static IServiceCollection AddUrsaServices(this IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IMenuConfigurationService, MenuConfigurationService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        return services;
    }
}
