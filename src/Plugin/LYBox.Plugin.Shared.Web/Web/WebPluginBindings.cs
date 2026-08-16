using LYBox.Plugin.Shared.Rpc;

namespace LYBox.Plugin.Shared.Web;

/// <summary>
/// Web 插件绑定注册帮助类。
/// </summary>
/// <remarks>
/// <para>
/// 扫描 <see cref="IWebPlugin"/> 所在程序集中所有 <see cref="IRpcBindingSource"/> 实现
/// （由源生成器针对 <c>[RpcCommand]</c> 类生成），调用其 <see cref="IRpcBindingSource.RegisterBindings"/>
/// 方法把命令注册到 IPC 主机。
/// </para>
/// <para>
/// 调用时机：在 <see cref="WebPluginView"/> 创建 <see cref="WebViewIpcHost"/> 后、
/// <see cref="WebViewIpcHost.InjectBindingsAsync"/> 前调用，确保命令清单已注册。
/// </para>
/// </remarks>
public static class WebPluginBindings
{
    /// <summary>
    /// 把插件程序集中所有 [RpcCommand] 类注册到 RPC 主机。
    /// </summary>
    /// <param name="host">RPC 主机（由 WebPluginView 创建的 WebViewIpcHost）。</param>
    /// <param name="plugin">Web 插件实例，其程序集被扫描以发现绑定类。</param>
    public static void Register(IRpcHost host, IWebPlugin plugin)
    {
        var asm = plugin.GetType().Assembly;
        foreach (var type in asm.GetExportedTypes())
        {
            if (type.IsAbstract || type.IsInterface) continue;
            if (!typeof(IRpcBindingSource).IsAssignableFrom(type)) continue;

            try
            {
                var bindingSource = (IRpcBindingSource?)Activator.CreateInstance(type);
                bindingSource?.RegisterBindings(host);
            }
            catch
            {
                // 单个绑定类注册失败不影响其他绑定类
            }
        }
    }
}
