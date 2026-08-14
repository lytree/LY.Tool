namespace LYBox.Plugin.Shared.Models;

public class PluginManifest
{
    public string? PluginId { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string? Assembly { get; set; }
    public List<string>? Dependencies { get; set; }

    /// <summary>
    /// Additional assembly name patterns that this plugin declares as shared
    /// (forwarded to the host's default AssemblyLoadContext). Each entry is either
    /// an exact assembly name or a prefix pattern ending with '*'.
    /// These are merged with the default shared-assemblies.txt list at runtime.
    /// </summary>
    public List<string>? SharedAssemblies { get; set; }

    public string? State { get; set; }
    public DateTime? InstallTime { get; set; }
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// 该插件所需的最低 Plugin SDK 契约版本。
    /// 缺省时视为 "0.0.0"（向后兼容未声明版本要求的旧插件）。
    /// 主体程序加载时与 PluginSdkContract.CurrentVersion 比对，不满足则拒绝加载。
    /// </summary>
    public string? MinPluginSdkVersion { get; set; }
}
