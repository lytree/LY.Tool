#nullable enable
namespace Avalonia.Plugin.Downloader.Resources;

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
                    "Avalonia.Plugin.Downloader.Resources.Strings",
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

    public static string NAV_Downloader => ResourceManager.GetString(nameof(NAV_Downloader), Culture)!;
    public static string NAV_Downloader_M3u8 => ResourceManager.GetString(nameof(NAV_Downloader_M3u8), Culture)!;
}
