#nullable enable
namespace LYBox.Plugin.DateTimeControls.Resources;

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
                    "LYBox.Plugin.DateTimeControls.Resources.Strings",
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

    public static string NAV_DateTime => ResourceManager.GetString(nameof(NAV_DateTime), Culture)!;
    public static string NAV_DatePicker => ResourceManager.GetString(nameof(NAV_DatePicker), Culture)!;
    public static string NAV_DateTimePicker => ResourceManager.GetString(nameof(NAV_DateTimePicker), Culture)!;
    public static string NAV_DateRangePicker => ResourceManager.GetString(nameof(NAV_DateRangePicker), Culture)!;
    public static string NAV_TimePicker => ResourceManager.GetString(nameof(NAV_TimePicker), Culture)!;
    public static string NAV_TimeRangePicker => ResourceManager.GetString(nameof(NAV_TimeRangePicker), Culture)!;
    public static string NAV_TimeBox => ResourceManager.GetString(nameof(NAV_TimeBox), Culture)!;
    public static string NAV_Clock => ResourceManager.GetString(nameof(NAV_Clock), Culture)!;
    public static string FMT_DateTimeDisplay => ResourceManager.GetString(nameof(FMT_DateTimeDisplay), Culture)!;
    public static string FMT_TimeDisplay => ResourceManager.GetString(nameof(FMT_TimeDisplay), Culture)!;
    public static string LBL_TimeStart => ResourceManager.GetString(nameof(LBL_TimeStart), Culture)!;
    public static string LBL_TimeEnd => ResourceManager.GetString(nameof(LBL_TimeEnd), Culture)!;
    public static string NAV_DateOffsetPicker => ResourceManager.GetString(nameof(NAV_DateOffsetPicker), Culture)!;
    public static string NAV_DateOffsetRangePicker => ResourceManager.GetString(nameof(NAV_DateOffsetRangePicker), Culture)!;
    public static string NAV_DateOnlyPicker => ResourceManager.GetString(nameof(NAV_DateOnlyPicker), Culture)!;
    public static string NAV_DateOnlyRangePicker => ResourceManager.GetString(nameof(NAV_DateOnlyRangePicker), Culture)!;
    public static string NAV_DateTimeOffsetPicker => ResourceManager.GetString(nameof(NAV_DateTimeOffsetPicker), Culture)!;
    public static string NAV_TimeOnlyPicker => ResourceManager.GetString(nameof(NAV_TimeOnlyPicker), Culture)!;
    public static string NAV_TimeOnlyRangePicker => ResourceManager.GetString(nameof(NAV_TimeOnlyRangePicker), Culture)!;
}
