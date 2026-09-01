using System.Reflection;

namespace LYBox.Plugin.Shared;

/// <summary>读取插件程序集的版本信息。</summary>
public static class PluginAssemblyVersion
{
    public static string For<TPlugin>() =>
        typeof(TPlugin).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
        ?? typeof(TPlugin).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";
}