using Avalonia.Data.Converters;
using LYBox.Plugin.Shared.Models;

namespace LYBox.Layout.Core.ViewModels;

/// <summary>
/// 插件状态相关值转换器，供 Ursa/Fluent 两个布局共享。
/// 注意：ObjectConverters.IsNotNull 已由 Avalonia 内置提供（Avalonia.Data.Converters.ObjectConverters），
/// XAML 中直接使用 {x:Static ObjectConverters.IsNotNull} 即可，无需在此重复定义。
/// </summary>
public static class PluginStateConverters
{
    public static readonly IValueConverter IsLoaded = new FuncValueConverter<PluginState, bool>(s => s == PluginState.Loaded);
    public static readonly IValueConverter IsNotLoaded = new FuncValueConverter<PluginState, bool>(s => s != PluginState.Loaded);
    public static readonly IValueConverter IsDisabled = new FuncValueConverter<PluginState, bool>(s => s == PluginState.Disabled);
    public static readonly IValueConverter IsPendingUninstall = new FuncValueConverter<PluginState, bool>(s => s == PluginState.PendingUninstall);
    public static readonly IValueConverter IsEmpty = new FuncValueConverter<int, bool>(c => c == 0);
}
