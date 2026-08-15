namespace LYBox.Plugin.Shared;

public interface IPluginMetadata
{
    /// <summary>
    /// 插件名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 插件版本
    /// </summary>
    string Version { get; }

    /// <summary>
    /// 插件作者
    /// </summary>
    string Author { get; }

    /// <summary>
    /// 插件描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 插件依赖
    /// </summary>
    IEnumerable<string> Dependencies { get; }

    /// <summary>
    /// 插件唯一标识
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// 该插件所需的最低 Plugin SDK 契约版本。
    /// 默认 "0.0.0" 表示无约束（向后兼容未声明的旧插件）。
    /// 主体程序加载时与 PluginSdkContract.CurrentVersion 比对，
    /// 若插件要求版本高于当前 SDK 版本则拒绝加载。
    /// </summary>
    string MinPluginSdkVersion => "0.0.0";

}


