#nullable enable
namespace Avalonia.Plugin.ScottPlot.Resources;

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
                    "Avalonia.Plugin.ScottPlot.Resources.Strings",
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

    public static string NAV_ScottPlot => ResourceManager.GetString(nameof(NAV_ScottPlot), Culture)!;
    public static string NAV_QuickStart => ResourceManager.GetString(nameof(NAV_QuickStart), Culture)!;
    public static string NAV_SignalPlot => ResourceManager.GetString(nameof(NAV_SignalPlot), Culture)!;
    public static string NAV_ScatterPlot => ResourceManager.GetString(nameof(NAV_ScatterPlot), Culture)!;
    public static string NAV_BarChart => ResourceManager.GetString(nameof(NAV_BarChart), Culture)!;
}
