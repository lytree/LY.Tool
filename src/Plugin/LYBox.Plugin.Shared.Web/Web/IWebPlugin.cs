namespace LYBox.Plugin.Shared.Web;

/// <summary>
/// Web 插件契约：在 <see cref="IPlugin"/> 基础上声明前端资源描述。
/// 由 <c>[GenerateMetadata]</c> 源生成器从 csproj 的
/// <c>PluginKind/PluginWwwroot/PluginEntryPage</c> 常量自动生成 <see cref="Web"/> 描述符。
/// </summary>
/// <remarks>
/// <para>
/// 该接口不破坏现有 <see cref="IPlugin"/> 契约，未实现此接口的传统插件不受影响。
/// Web 插件的 <c>wwwroot</c> 注册由宿主 <c>PluginLoader.RegisterWebPlugins</c>
/// 依据 manifest（csproj 单一事实来源）统一完成，插件代码中不再出现任何注册调用。
/// </para>
/// </remarks>
public interface IWebPlugin : IPlugin
{
    /// <summary>Web 前端资源描述符（由源生成器从 csproj 常量生成）。</summary>
    IWebPluginDescriptor Web { get; }
}

/// <summary>
/// Web 插件前端资源描述符。宿主据此拼接安装路径完成 <see cref="WebHostService.MapPluginRoot"/>。
/// </summary>
public interface IWebPluginDescriptor
{
    /// <summary>前端资源根目录名称（相对插件安装目录），默认 "wwwroot"。</summary>
    string WwwrootName { get; }

    /// <summary>入口页面文件名，默认 "index.html"。</summary>
    string EntryPage { get; }
}

/// <summary>
/// 默认 <see cref="IWebPluginDescriptor"/> 实现，由源生成器实例化。
/// </summary>
public sealed class WebPluginDescriptor : IWebPluginDescriptor
{
    public WebPluginDescriptor(string wwwrootName, string entryPage)
    {
        WwwrootName = wwwrootName;
        EntryPage = entryPage;
    }

    public string WwwrootName { get; }

    public string EntryPage { get; }
}