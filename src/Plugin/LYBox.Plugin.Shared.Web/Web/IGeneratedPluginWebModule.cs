using LYBox.Plugin.Shared.Rpc;

namespace LYBox.Plugin.Shared.Web;

/// <summary>Optional generated module hook that registers RPC bindings without scanning an assembly.</summary>
public interface IGeneratedPluginWebModule
{
    void RegisterRpcBindings(IRpcHost host, IServiceProvider services);
}
