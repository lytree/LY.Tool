using LYBox.Plugin.Shared.Rpc;

namespace LYBox.Plugin.Shared.Web;

/// <summary>
/// Web 插件绑定注册帮助类。
/// </summary>
/// <remarks>
/// <para>
/// 通过插件的 generated module 注册 RPC bindings，不扫描插件程序集。
/// </para>
/// <para>
/// 调用时机：在 <see cref="WebPluginView"/> 创建 <see cref="WebViewIpcHost"/> 后、
/// <see cref="WebViewIpcHost.InjectBindingsAsync"/> 前调用，确保命令清单已注册。
/// </para>
/// </remarks>
public static class WebPluginBindings
{
    /// <summary>
    /// 把 generated module 中声明的 [RpcCommand] bindings 注册到 RPC 主机。
    /// </summary>
    /// <param name="host">RPC 主机（由 WebPluginView 创建的 WebViewIpcHost）。</param>
    public static void Register(
        IRpcHost host,
        IGeneratedPluginWebModule module,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(services);
        module.RegisterRpcBindings(host, services);
    }
}
