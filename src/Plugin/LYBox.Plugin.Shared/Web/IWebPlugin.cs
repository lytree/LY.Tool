namespace LYBox.Plugin.Shared.Web;

/// <summary>
/// Web 插件契约：在 <see cref="IPlugin"/> 基础上额外声明前端资源位置。
/// 实现此接口的插件应在 <c>RegisterAsync</c> 阶段主动调用
/// <c>serviceProvider.GetService&lt;WebHostService&gt;()?.MapPluginRoot(PluginId, WwwrootPath)</c>
/// 注册其前端资源，否则 <see cref="WebPluginView"/> 将不会渲染 WebView。
/// 宿主在 <c>RegisterAsync</c> 之前会自动调用 <c>PluginLoader.InjectWebPluginBaseDirs()</c>
/// 注入 <see cref="PluginBaseDir"/>，使 <see cref="WwwrootPath"/> 正确计算。
/// </summary>
/// <remarks>
/// <para>
/// 该接口不破坏现有 <see cref="IPlugin"/> 契约，未实现此接口的传统插件不受影响。
/// 未主动注册的插件不会被 WebHostService 服务，其 WebPluginView 页面也将显示占位提示而非 WebView。
/// </para>
/// </remarks>
public interface IWebPlugin : IPlugin
{
    /// <summary>插件程序集所在目录（绝对路径，由宿主在加载时注入）。</summary>
    string PluginBaseDir { get; set; }

    /// <summary>
    /// 前端资源根目录（绝对路径）。默认实现返回 <c>Path.Combine(PluginBaseDir, "wwwroot")</c>。
    /// </summary>
    string WwwrootPath => Path.Combine(PluginBaseDir, "wwwroot");

    /// <summary>Web 入口页面文件名（默认 <c>index.html</c>）。</summary>
    string EntryPage => "index.html";
}
