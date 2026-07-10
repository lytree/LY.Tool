#nullable enable
namespace LYBox.Plugin.LayoutDisplay.Resources;

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
                    "LYBox.Plugin.LayoutDisplay.Resources.Strings",
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

    public static string NAV_LayoutDisplay => ResourceManager.GetString(nameof(NAV_LayoutDisplay), Culture)!;
    public static string NAV_Badge => ResourceManager.GetString(nameof(NAV_Badge), Culture)!;
    public static string NAV_Banner => ResourceManager.GetString(nameof(NAV_Banner), Culture)!;
    public static string NAV_Avatar => ResourceManager.GetString(nameof(NAV_Avatar), Culture)!;
    public static string NAV_AspectRatioLayout => ResourceManager.GetString(nameof(NAV_AspectRatioLayout), Culture)!;
    public static string NAV_Descriptions => ResourceManager.GetString(nameof(NAV_Descriptions), Culture)!;
    public static string NAV_DisableContainer => ResourceManager.GetString(nameof(NAV_DisableContainer), Culture)!;
    public static string NAV_Divider => ResourceManager.GetString(nameof(NAV_Divider), Culture)!;
    public static string NAV_DualBadge => ResourceManager.GetString(nameof(NAV_DualBadge), Culture)!;
    public static string NAV_ElasticWrapPanel => ResourceManager.GetString(nameof(NAV_ElasticWrapPanel), Culture)!;
    public static string NAV_ImageViewer => ResourceManager.GetString(nameof(NAV_ImageViewer), Culture)!;
    public static string NAV_Marquee => ResourceManager.GetString(nameof(NAV_Marquee), Culture)!;
    public static string NAV_NumberDisplayer => ResourceManager.GetString(nameof(NAV_NumberDisplayer), Culture)!;
    public static string NAV_QrCode => ResourceManager.GetString(nameof(NAV_QrCode), Culture)!;
    public static string NAV_ScrollToButton => ResourceManager.GetString(nameof(NAV_ScrollToButton), Culture)!;
    public static string NAV_Timeline => ResourceManager.GetString(nameof(NAV_Timeline), Culture)!;
    public static string NAV_TwoTonePathIcon => ResourceManager.GetString(nameof(NAV_TwoTonePathIcon), Culture)!;
    public static string LBL_Step1 => ResourceManager.GetString(nameof(LBL_Step1), Culture)!;
    public static string LBL_Step2 => ResourceManager.GetString(nameof(LBL_Step2), Culture)!;
    public static string LBL_Step3 => ResourceManager.GetString(nameof(LBL_Step3), Culture)!;
    public static string LBL_Step4 => ResourceManager.GetString(nameof(LBL_Step4), Culture)!;
    public static string LBL_Step5 => ResourceManager.GetString(nameof(LBL_Step5), Culture)!;
    public static string LBL_ActualUsers => ResourceManager.GetString(nameof(LBL_ActualUsers), Culture)!;
    public static string LBL_7DayRetention => ResourceManager.GetString(nameof(LBL_7DayRetention), Culture)!;
    public static string LBL_SecurityLevel => ResourceManager.GetString(nameof(LBL_SecurityLevel), Culture)!;
    public static string LBL_Level3 => ResourceManager.GetString(nameof(LBL_Level3), Culture)!;
    public static string LBL_VerticalTag => ResourceManager.GetString(nameof(LBL_VerticalTag), Culture)!;
    public static string LBL_ECommerce => ResourceManager.GetString(nameof(LBL_ECommerce), Culture)!;
    public static string LBL_CertStatus => ResourceManager.GetString(nameof(LBL_CertStatus), Culture)!;
    public static string LBL_NotCertified => ResourceManager.GetString(nameof(LBL_NotCertified), Culture)!;
    public static string BTN_TestSetNull => ResourceManager.GetString(nameof(BTN_TestSetNull), Culture)!;
    public static string NAV_GroupBox => ResourceManager.GetString(nameof(NAV_GroupBox), Culture)!;
    public static string NAV_VirtualizingUniformGrid => ResourceManager.GetString(nameof(NAV_VirtualizingUniformGrid), Culture)!;
    public static string NAV_ThemeVariantMapper => ResourceManager.GetString(nameof(NAV_ThemeVariantMapper), Culture)!;
}
