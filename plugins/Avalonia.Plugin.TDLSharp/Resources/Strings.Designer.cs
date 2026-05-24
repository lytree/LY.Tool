#nullable enable
namespace Avalonia.Plugin.TDLSharp.Resources;

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
                    "Avalonia.Plugin.TDLSharp.Resources.Strings",
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

    public static string NAV_TDL => ResourceManager.GetString(nameof(NAV_TDL), Culture)!;
    public static string NAV_TDL_BatchForward => ResourceManager.GetString(nameof(NAV_TDL_BatchForward), Culture)!;
    public static string NAV_TDL_ClearMessage => ResourceManager.GetString(nameof(NAV_TDL_ClearMessage), Culture)!;
    public static string NAV_TDL_DeepCopy => ResourceManager.GetString(nameof(NAV_TDL_DeepCopy), Culture)!;
    public static string NAV_TDL_MessageExport => ResourceManager.GetString(nameof(NAV_TDL_MessageExport), Culture)!;
}
