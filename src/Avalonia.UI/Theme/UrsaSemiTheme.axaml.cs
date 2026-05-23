using System.Collections;
using System.Globalization;
using System.Resources;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Services;
using Avalonia.UI.Resources;
using Avalonia.Styling;
using Avalonia.UI.Theme.Animations;

namespace Avalonia.UI.Theme;

public partial class UrsaSemiTheme : Styles
{
    private static UrsaSemiTheme? _instance;

    public static UrsaSemiTheme? Instance => _instance;

    public UrsaSemiTheme(IServiceProvider? provider = null)
    {
        AvaloniaXamlLoader.Load(provider, this);
        Resources.MergedDictionaries.Add(new DefaultSizeAnimations());
        Resources.MergedDictionaries.Add(new NavMenuSizeAnimations());

        var systemCulture = CultureInfo.CurrentUICulture;
        LoadLocaleFromResx(systemCulture);

        _instance = this;
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
                var resourceKey = $"STRING_{entry.Key}";
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
            var resourceKey = $"STRING_{entry.Key}";
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
            var resourceKey = $"STRING_{entry.Key}";
            element.Resources[resourceKey] = s;
            if (_instance is not null)
            {
                _instance.Resources[resourceKey] = s;
            }
        }

        SyncLocalizationService(culture);
    }
}
