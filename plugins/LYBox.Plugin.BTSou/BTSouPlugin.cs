using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LYBox.Plugin.BTSou;

/// <summary>
/// BTSOU 插件入口（精简版：仅保留资源搜索 + 迅雷下载）。
/// 已移除数据库授权锁/举报系统。
/// </summary>
[GenerateMetadata]
public partial class BTSouPlugin
{
    public Task InitializeAsync(IServiceCollection services)
    {
        // 服务由 DI 容器管理生命周期；ViewModel 经 ServiceLocator 获取实例
        services.AddSingleton<Services.BTSouSearchService>();
        return Task.CompletedTask;
    }
}
