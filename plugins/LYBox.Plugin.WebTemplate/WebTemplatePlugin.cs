using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Web;

namespace LYBox.Plugin.WebTemplate;

/// <summary>
/// Web 模板插件。前端资源注册由宿主 <c>PluginLoader.RegisterWebPlugins</c>
/// 依据 manifest（csproj 声明的 PluginKind/PluginWwwroot/PluginEntryPage）
/// 统一完成，无需插件代码手动调用 MapPluginRoot（S2 BC-2/BC-3）。
/// <see cref="IWebPlugin"/> 的 <c>Web</c> 描述符由 [GenerateMetadata] 源生成器生成。
/// </summary>
[GenerateMetadata]
public partial class WebTemplatePlugin : IWebPlugin
{
}