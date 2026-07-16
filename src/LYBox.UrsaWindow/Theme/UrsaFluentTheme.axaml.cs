using System.Collections;
using System.Globalization;
using System.Resources;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;
using LYBox.UrsaWindow.Resources;
using Avalonia.Media;
using Avalonia.Styling;
using LYBox.UrsaWindow.Theme.Animations;

namespace LYBox.UrsaWindow.Theme;

public partial class UrsaFluentTheme : Styles
{
    private static UrsaFluentTheme? _instance;

    public static UrsaFluentTheme? Instance => _instance;

    public UrsaFluentTheme(IServiceProvider? provider = null)
    {
        AvaloniaXamlLoader.Load(provider, this);

        // FontFamily 不支持 XAML 元素语法实例化，需在代码中注册
        // 使用跨平台回退链：Windows → macOS → Linux → 通用
        Resources["FluentFontFamilyRegular"] = new FontFamily(
            "Microsoft YaHei, PingFang SC, Noto Sans CJK SC, WenQuanYi Micro Hei, sans-serif");
        Resources["CodeFontFamily"] = new FontFamily(
            "Cascadia Code, Consolas, SF Mono, Menlo, DejaVu Sans Mono, Inconsolata, monospace");

        Resources.MergedDictionaries.Add(new DefaultSizeAnimations());
        Resources.MergedDictionaries.Add(new NavMenuSizeAnimations());

        var systemCulture = CultureInfo.CurrentUICulture;
        _instance = this;
        LoadLocaleFromResx(systemCulture);
    }

    public CultureInfo? Locale
    {
        get;
        set
        {
            try
            {
                field = value ?? new CultureInfo("en-US");
                LoadLocaleFromResx(field);
                SyncLocalizationService(field);
            }
            catch
            {
                field = CultureInfo.InvariantCulture;
            }
        }
    }

    private static void LoadLocaleFromResx(CultureInfo culture)
    {
        if (_instance is null) return;

        var resolvedCulture = ResolveCulture(culture);
        var resourceSet = Strings.ResourceManager.GetResourceSet(resolvedCulture, true, true);
        if (resourceSet is null)
        {
            resourceSet = Strings.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
        }

        if (resourceSet is not null)
        {
            foreach (DictionaryEntry entry in resourceSet)
            {
                if (entry.Value is not string s) continue;
                var resourceKey = $"{entry.Key}";
                _instance.Resources[resourceKey] = s;
            }
        }
    }

    private static CultureInfo ResolveCulture(CultureInfo? culture)
    {
        if (culture is null || Equals(culture, CultureInfo.InvariantCulture))
            return new CultureInfo("en-US");

        try
        {
            var resourceSet = Strings.ResourceManager.GetResourceSet(culture, true, false);
            if (resourceSet is not null)
                return culture;
        }
        catch { }

        if (culture.Parent != null && !Equals(culture.Parent, CultureInfo.InvariantCulture))
        {
            try
            {
                var resourceSet = Strings.ResourceManager.GetResourceSet(culture.Parent, true, false);
                if (resourceSet is not null)
                    return culture.Parent;
            }
            catch { }
        }

        return new CultureInfo("en-US");
    }

    private static void SyncLocalizationService(CultureInfo culture)
    {
        try
        {
            if (ServiceLocator.TryGetService<ILocalizationService>(out var service) && service is not null)
            {
                service.SetCulture(culture);
            }
        }
        catch
        {
        }
    }

    public static void OverrideLocaleResources(Application application, CultureInfo? culture)
    {
        if (culture is null) return;

        var resolvedCulture = ResolveCulture(culture);
        var resourceSet = Strings.ResourceManager.GetResourceSet(resolvedCulture, true, true);
        if (resourceSet is null) return;

        foreach (DictionaryEntry entry in resourceSet)
        {
            if (entry.Value is not string s) continue;
            var resourceKey = $"{entry.Key}";
            application.Resources[resourceKey] = s;
            if (_instance is not null)
            {
                _instance.Resources[resourceKey] = s;
            }
        }

        SyncLocalizationService(culture);
    }

    public static void OverrideLocaleResources(StyledElement element, CultureInfo? culture)
    {
        if (culture is null) return;

        var resolvedCulture = ResolveCulture(culture);
        var resourceSet = Strings.ResourceManager.GetResourceSet(resolvedCulture, true, true);
        if (resourceSet is null) return;

        foreach (DictionaryEntry entry in resourceSet)
        {
            if (entry.Value is not string s) continue;
            var resourceKey = $"{entry.Key}";
            element.Resources[resourceKey] = s;
            if (_instance is not null)
            {
                _instance.Resources[resourceKey] = s;
            }
        }

        SyncLocalizationService(culture);
    }
}
