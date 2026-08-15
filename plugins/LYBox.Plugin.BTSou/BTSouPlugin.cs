using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LYBox.Plugin.BTSou;

/// <summary>
/// BTSOU 插件入口。
/// 业务逻辑移植自 BTSOU v24.10.24（KiongChan），
/// 包含 BT 资源搜索（资源池解析/关键词匹配/磁力链接生成）与
/// MySQL 授权锁、举报系统。
/// </summary>
[GenerateMetadata]
public partial class BTSouPlugin : IPluginMetadata
{
    public string Name => "BTSou Search";
    public string Version => "1.0.0";
    public string Author => "KiongChan";
    public string Description => "BTSOU 业务逻辑移植：BT 资源搜索 + MySQL 授权锁/举报系统（源自反编译分析，仅用于学习研究）。";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "BTSOU-PLUGIN-2026-0000-000000000001";

    public Task InitializeAsync(IServiceCollection services)
    {
        // 服务以静态单例为主（ViewModel 由生成器无参构造），此处注册便于其他插件/宿主按需获取
        services.AddSingleton(Services.BTSouDatabaseService.Current);
        services.AddSingleton(Services.BTSouSearchService.Current);
        return Task.CompletedTask;
    }

    public Task RegisterAsync(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService<ILocalizationService>() is { } loc)
            loc.RegisterResourceManager(Resources.Strings.ResourceManager);
        return Task.CompletedTask;
    }
}
