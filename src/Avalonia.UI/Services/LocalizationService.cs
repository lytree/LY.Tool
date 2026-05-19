using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;
using Avalonia.Controls;
using Avalonia.Plugin.Shared.Resources;
using Avalonia.Plugin.Shared.Services;
using Avalonia.Styling;

namespace Avalonia.UI.Services;

public class LocalizationService : ILocalizationService
{
    private readonly ConcurrentDictionary<string, (string? LookupPrefix, ResourceManager Manager)> _resourceManagers = new();
    private CultureInfo _currentCulture = new("en-US");

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

    public void SetCulture(CultureInfo culture)
    {
        if (Equals(_currentCulture, culture))
            return;

        _currentCulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        SyncToResourceDictionary();

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

        foreach (var (_, (lookupPrefix, manager)) in _resourceManagers)
        {
            using var resourceSet = manager.GetResourceSet(_currentCulture, true, true);
            if (resourceSet is null) continue;

            foreach (DictionaryEntry entry in resourceSet)
            {
                if (entry.Value is not string s) continue;
                var resourceKey = string.IsNullOrEmpty(lookupPrefix)
                    ? $"STRING_{entry.Key}"
                    : $"STRING_{lookupPrefix}_{entry.Key}";
                app.Resources[resourceKey] = s;
            }
        }
    }
}
