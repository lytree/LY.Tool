using Avalonia.Data.Converters;
using LYBox.Plugin.Shared.Models;

namespace LYBox.UrsaWindow.ViewModels;

public static class PluginStateConverters
{
    public static readonly IValueConverter IsLoaded = new FuncValueConverter<PluginState, bool>(s => s == PluginState.Loaded);
    public static readonly IValueConverter IsNotLoaded = new FuncValueConverter<PluginState, bool>(s => s != PluginState.Loaded);
    public static readonly IValueConverter IsDisabled = new FuncValueConverter<PluginState, bool>(s => s == PluginState.Disabled);
    public static readonly IValueConverter IsPendingUninstall = new FuncValueConverter<PluginState, bool>(s => s == PluginState.PendingUninstall);
    public static readonly IValueConverter IsEmpty = new FuncValueConverter<int, bool>(c => c == 0);
}

public static class ObjectConverters
{
    public static readonly IValueConverter IsNotNull = new FuncValueConverter<object?, bool>(o => o != null);
}
