#nullable enable
namespace Avalonia.Plugin.ProDataGrid.Resources;

public static class Strings
{
    private static global::System.Resources.ResourceManager? _resourceManager;

    public static global::System.Resources.ResourceManager ResourceManager
    {
        get
        {
            if (_resourceManager is null)
            {
                _resourceManager = new global::System.Resources.ResourceManager(
                    "Avalonia.Plugin.ProDataGrid.Resources.Strings",
                    typeof(Strings).Assembly);
            }
            return _resourceManager;
        }
    }

    private static global::System.Globalization.CultureInfo? _culture;

    public static global::System.Globalization.CultureInfo Culture
    {
        get => _culture ?? global::System.Globalization.CultureInfo.CurrentUICulture;
        set => _culture = value;
    }

    public static string NAV_ProDataGrid => ResourceManager.GetString(nameof(NAV_ProDataGrid), Culture)!;
    public static string NAV_BasicDataGrid => ResourceManager.GetString(nameof(NAV_BasicDataGrid), Culture)!;
    public static string NAV_FormulaDataGrid => ResourceManager.GetString(nameof(NAV_FormulaDataGrid), Culture)!;
    public static string NAV_ColumnTypes => ResourceManager.GetString(nameof(NAV_ColumnTypes), Culture)!;
    public static string NAV_FilteringGrouping => ResourceManager.GetString(nameof(NAV_FilteringGrouping), Culture)!;
    public static string NAV_RowDetailsSelection => ResourceManager.GetString(nameof(NAV_RowDetailsSelection), Culture)!;
    public static string NAV_DragDrop => ResourceManager.GetString(nameof(NAV_DragDrop), Culture)!;
    public static string NAV_CustomDrawingEditing => ResourceManager.GetString(nameof(NAV_CustomDrawingEditing), Culture)!;
    public static string NAV_CustomDrawingLiveUpdates => ResourceManager.GetString(nameof(NAV_CustomDrawingLiveUpdates), Culture)!;
    public static string NAV_EditingDemo => ResourceManager.GetString(nameof(NAV_EditingDemo), Culture)!;
}
