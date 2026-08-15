namespace LYBox.Plugin.Shared.Web;

/// <summary>
/// Web 插件契约：在 <see cref="IPlugin"/> 基础上额外声明前端资源位置。
/// </summary>
/// <remarks>
/// <para>
/// 实现此接口的插件会被 <c>PluginLoader.GetWebPluginRoots()</c> 扫描到，
/// 其 <see cref="WwwrootPath"/> 指向的目录会被 <c>WebHostService.MapPluginRoot</c>
/// 注册到 Kestrel 的路由 <c>/{pluginId}/...</c> 下。
/// </para>
/// <para>
/// 默认 <see cref="WwwrootPath"/> 由 <see cref="PluginBaseDir"/> 拼接 <c>wwwroot</c> 子目录得到。
/// 插件作者可在 <c>RegisterAsync</c> 阶段动态修改 <see cref="WwwrootPath"/>
/// （例如指向用户自定义目录），但必须在 <c>WebHostService</c> 启动前完成。
/// </para>
/// <para>
/// 该接口不破坏现有 <see cref="IPlugin"/> 契约，未实现此接口的传统插件不受影响。
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
