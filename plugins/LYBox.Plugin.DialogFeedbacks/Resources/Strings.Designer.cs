#nullable enable
namespace LYBox.Plugin.DialogFeedbacks.Resources;

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
                    "LYBox.Plugin.DialogFeedbacks.Resources.Strings",
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

    public static string NAV_DialogFeedbacks => ResourceManager.GetString(nameof(NAV_DialogFeedbacks), Culture)!;
    public static string NAV_Dialog => ResourceManager.GetString(nameof(NAV_Dialog), Culture)!;
    public static string NAV_Drawer => ResourceManager.GetString(nameof(NAV_Drawer), Culture)!;
    public static string NAV_MessageBox => ResourceManager.GetString(nameof(NAV_MessageBox), Culture)!;
    public static string NAV_Toast => ResourceManager.GetString(nameof(NAV_Toast), Culture)!;
    public static string NAV_Skeleton => ResourceManager.GetString(nameof(NAV_Skeleton), Culture)!;
    public static string NAV_PopConfirm => ResourceManager.GetString(nameof(NAV_PopConfirm), Culture)!;
    public static string NAV_Notification => ResourceManager.GetString(nameof(NAV_Notification), Culture)!;
    public static string NAV_Loading => ResourceManager.GetString(nameof(NAV_Loading), Culture)!;
    public static string LBL_DefaultPopConfirm => ResourceManager.GetString(nameof(LBL_DefaultPopConfirm), Culture)!;
    public static string LBL_FocusTriggerMode => ResourceManager.GetString(nameof(LBL_FocusTriggerMode), Culture)!;
    public static string LBL_NonButtonElement => ResourceManager.GetString(nameof(LBL_NonButtonElement), Culture)!;
    public static string LBL_AsyncCommandSupport => ResourceManager.GetString(nameof(LBL_AsyncCommandSupport), Culture)!;
    public static string LBL_ChickenOrEgg => ResourceManager.GetString(nameof(LBL_ChickenOrEgg), Culture)!;
    public static string LBL_Craftsmanship => ResourceManager.GetString(nameof(LBL_Craftsmanship), Culture)!;
    public static string LBL_MountainWater => ResourceManager.GetString(nameof(LBL_MountainWater), Culture)!;
    public static string POP_ConfirmSaveHeader => ResourceManager.GetString(nameof(POP_ConfirmSaveHeader), Culture)!;
    public static string POP_IrreversibleChange => ResourceManager.GetString(nameof(POP_IrreversibleChange), Culture)!;
    public static string NAV_OverlayDialog => ResourceManager.GetString(nameof(NAV_OverlayDialog), Culture)!;
    public static string NAV_WindowDialog => ResourceManager.GetString(nameof(NAV_WindowDialog), Culture)!;
}
