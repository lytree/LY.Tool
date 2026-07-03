#nullable enable
namespace LYBox.Plugin.Downloader.Resources;

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
                    "LYBox.Plugin.Downloader.Resources.Strings",
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

    /// <summary>动态获取资源字符串，支持格式化参数。未找到时返回 key 本身。</summary>
    public static string Get(string key, params object[] args)
    {
        var value = ResourceManager.GetString(key, Culture) ?? key;
        return args.Length > 0 ? string.Format(Culture, value, args) : value;
    }
}
