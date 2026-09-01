namespace LYBox.Plugin.Shared;

/// <summary>
/// 插件可自定义其承载窗口标题栏的附加内容。
/// </summary>
public interface IPluginTitleBarContentProvider
{
    object? TitleBarContent { get; }
}