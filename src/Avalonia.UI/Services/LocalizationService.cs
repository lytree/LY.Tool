using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;
using Avalonia.Plugin.Shared.Services;
using Avalonia.UI.Resources;
using Avalonia.UI.Theme;

namespace Avalonia.UI.Services;

public class LocalizationService : ILocalizationService
{
    private readonly ConcurrentDictionary<string, (string? LookupPrefix, ResourceManager Manager)> _resourceManagers = new();
    private CultureInfo _currentCulture = new("en-US");
    private bool _initialSync = true;

    public CultureInfo CurrentCulture => _currentCulture;

    public event EventHandler<CultureInfo>? CultureChanged;

    public LocalizationService()
    {
        RegisterResourceManager(Strings.ResourceManager, string.Empty);
    }

    public string GetString(string key)
    {
        foreach (var (_, (lookupPrefix, manager)) in _resourceManagers)
        {
            var lookupKey = string.IsNullOrEmpty(lookupPrefix) ? key : $"{lookupPrefix}_{key}";
            var value = manager.GetString(lookupKey, _currentCulture);
            if (value is not null)
                return value;
        }

        return key;
    }

    public string GetString(string key, string fallback)
    {
        foreach (var (_, (lookupPrefix, manager)) in _resourceManagers)
        {
            var lookupKey = string.IsNullOrEmpty(lookupPrefix) ? key : $"{lookupPrefix}_{key}";
            var value = manager.GetString(lookupKey, _currentCulture);
            if (value is not null)
                return value;
        }

        return fallback;
    }

    public string GetString(string key, params object[] args)
    {
        var format = GetString(key);
        try
        {
            return string.Format(_currentCulture, format, args);
        }
        catch
        {
            return format;
        }
    }

    public void SetCulture(CultureInfo culture)
    {
        var cultureChanged = !Equals(_currentCulture, culture);
        if (!cultureChanged && !_initialSync)
            return;

        _currentCulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        SyncToResourceDictionary();

        _initialSync = false;

        if (cultureChanged)
            CultureChanged?.Invoke(this, culture);
    }

    public void RegisterResourceManager(ResourceManager manager, string? prefix = null)
    {
        var dictKey = manager.BaseName;
        _resourceManagers[dictKey] = (prefix, manager);
    }

    private void SyncToResourceDictionary()
    {
        var app = Application.Current;
        if (app is null) return;

        var themeInstance = UrsaSemiTheme.Instance;

        foreach (var (_, (lookupPrefix, manager)) in _resourceManagers)
        {
            var resourceSet = manager.GetResourceSet(_currentCulture, true, true);
            if (resourceSet is null) continue;

            foreach (DictionaryEntry entry in resourceSet)
            {
                if (entry.Value is not string s) continue;
                var resourceKey = string.IsNullOrEmpty(lookupPrefix)
                    ? $"STRING_{entry.Key}"
                    : $"STRING_{lookupPrefix}_{entry.Key}";
                app.Resources[resourceKey] = s;
                if (themeInstance is not null)
                {
                    themeInstance.Resources[resourceKey] = s;
                }
            }
        }
    }
}
