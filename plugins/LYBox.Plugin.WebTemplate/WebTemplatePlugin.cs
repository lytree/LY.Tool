using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Web;
using Microsoft.Extensions.DependencyInjection;

namespace LYBox.Plugin.WebTemplate;

[GenerateMetadata]
public partial class WebTemplatePlugin : IPluginMetadata, IWebPlugin
{
    public string Name => "Web Template";
    public string Version => "1.0.0";
    public string Author => "AvaloniaTemplate";
    public string Description => "WebView + HTTP + IPC + SSE demo plugin with vanilla HTML/JS frontend";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d";

    // IWebPlugin：由宿主 PluginLoader.InjectWebPluginBaseDirs() 注入插件安装路径
    public string PluginBaseDir { get; set; } = string.Empty;

    public Task InitializeAsync(IServiceCollection services) => Task.CompletedTask;

    public Task RegisterAsync(IServiceProvider serviceProvider)
    {
        // 主动注册：将本插件的 wwwroot 注册到 WebHostService，触发懒加载启动。
        // 若不主动注册，WebHostService 保持关闭，WebTemplatePage 也不会渲染 WebView。
        serviceProvider.GetService<WebHostService>()?.MapPluginRoot(PluginId, ((IWebPlugin)this).WwwrootPath);
        return Task.CompletedTask;
    }
}
