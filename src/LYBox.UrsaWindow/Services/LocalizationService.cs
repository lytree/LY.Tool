using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;
using Avalonia;
using LYBox.Plugin.Shared.Services;
using LYBox.UrsaWindow.Resources;
using LYBox.UrsaWindow.Theme;

namespace LYBox.UrsaWindow.Services;

public sealed class LocalizationService : ILocalizationService
{
    private readonly ConcurrentDictionary<string, (string? LookupPrefix, ResourceManager Manager)> _resourceManagers = new();
    private CultureInfo _currentCulture = new("zh-CN");
    private bool _initialSync = true;
    private ConcurrentDictionary<string, string>? _stringCache;
    // 跟踪已注册到 Application.Resources 的键，切换文化时清理残留避免内存累积
    private HashSet<string>? _registeredResourceKeys;

    public CultureInfo CurrentCulture => _currentCulture;

    public event EventHandler<CultureInfo>? CultureChanged;

    public LocalizationService()
    {
        RegisterResourceManager(Strings.ResourceManager, string.Empty);
    }

    public string GetString(string key)
    {
        var cache = _stringCache;
        if (cache is not null && cache.TryGetValue(key, out var cached))
            return cached;

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
        var cache = _stringCache;
        if (cache is not null && cache.TryGetValue(key, out var cached))
            return cached;

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
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        _stringCache = null;
        RebuildCacheAndSyncResources();

        _initialSync = false;

        if (cultureChanged)
            CultureChanged?.Invoke(this, culture);
    }

    public void RegisterResourceManager(ResourceManager manager, string? prefix = null)
    {
        var dictKey = manager.BaseName;
        _resourceManagers[dictKey] = (prefix, manager);
        _stringCache = null;

        if (!_initialSync)
            RebuildCacheAndSyncResources();
    }

    private void RebuildCacheAndSyncResources()
    {
        var app = Application.Current;
        var themeInstance = UrsaFluentTheme.Instance;
        var cache = new ConcurrentDictionary<string, string>();

        // 记录本次注册的资源键，便于切换文化时清理上一轮残留键，避免 Application.Resources
        // 长期累积不同文化的键值对导致内存增长。
        var registeredKeys = new HashSet<string>();

        foreach (var (_, (lookupPrefix, manager)) in _resourceManagers)
        {
            var resourceSet = manager.GetResourceSet(_currentCulture, true, true);
            if (resourceSet is null) continue;

            foreach (DictionaryEntry entry in resourceSet)
            {
                if (entry.Value is not string s) continue;
                var entryKey = entry.Key?.ToString() ?? string.Empty;
                var resourceKey = string.IsNullOrEmpty(lookupPrefix)
                    ? entryKey
                    : $"{lookupPrefix}_{entryKey}";

                cache.TryAdd(entryKey, s);
                registeredKeys.Add(resourceKey);

                if (app is not null)
                {
                    app.Resources[resourceKey] = s;
                    if (themeInstance is not null)
                    {
                        themeInstance.Resources[resourceKey] = s;
                    }
                }
            }
        }

        // 清理上一轮文化切换残留的资源键（仅清理我们注册的键，避免误删其他来源）
        if (app is not null && _registeredResourceKeys is not null)
        {
            var stale = _registeredResourceKeys.Except(registeredKeys);
            foreach (var key in stale)
            {
                app.Resources.Remove(key);
                if (themeInstance is not null)
                {
                    themeInstance.Resources.Remove(key);
                }
            }
        }
        _registeredResourceKeys = registeredKeys;

        _stringCache = cache;
    }
}
