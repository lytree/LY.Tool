using System.Collections.Concurrent;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Models;
using Avalonia.Plugin.Shared.Services;
using Avalonia.UI.Data;
using Microsoft.EntityFrameworkCore;

namespace Avalonia.UI.Services;

public class SettingsService : ISettingsService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILocalizationService? _localizationService;
    private ConcurrentDictionary<string, SettingItem>? _settingsCache;
    private bool _cacheInitialized;

    public SettingsService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        _localizationService = ServiceLocator.GetService<ILocalizationService>();
    }

    private ConcurrentDictionary<string, SettingItem> EnsureCache()
    {
        if (_cacheInitialized) return _settingsCache!;

        using var db = _dbFactory.CreateDbContext();
        var items = db.Settings.OrderBy(s => s.GroupOrder).ThenBy(s => s.ItemOrder).ToList();
        _settingsCache = new ConcurrentDictionary<string, SettingItem>(
            items.Select(i => new KeyValuePair<string, SettingItem>(i.Key, i)));
        _cacheInitialized = true;
        return _settingsCache;
    }

    private void InvalidateCache()
    {
        _cacheInitialized = false;
        _settingsCache = null;
    }

    public void RegisterSetting(SettingDefinition definition)
    {
        using var db = _dbFactory.CreateDbContext();
        var existing = db.Settings.FirstOrDefault(s => s.Key == definition.Key);
        if (existing != null)
        {
            existing.DisplayName = definition.DisplayName;
            existing.Description = definition.Description;
            existing.GroupName = definition.GroupName;
            existing.GroupOrder = definition.GroupOrder;
            existing.ItemOrder = definition.ItemOrder;
            existing.SettingType = definition.SettingType;
            existing.DefaultValue = definition.DefaultValue;
            existing.PluginId = definition.PluginId;
            if (definition.Options != null)
                existing.SetOptions(definition.Options);
            db.SaveChanges();
            InvalidateCache();
            return;
        }

        var item = new SettingItem
        {
            Key = definition.Key,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            GroupName = definition.GroupName,
            GroupOrder = definition.GroupOrder,
            ItemOrder = definition.ItemOrder,
            SettingType = definition.SettingType,
            DefaultValue = definition.DefaultValue,
            PluginId = definition.PluginId,
            IsFolder = definition.IsFolder,
            RawValue = definition.DefaultValue ?? string.Empty
        };
        if (definition.Options != null)
            item.SetOptions(definition.Options);

        db.Settings.Add(item);
        db.SaveChanges();
        InvalidateCache();
    }

    public void RegisterSettings(IEnumerable<SettingDefinition> definitions)
    {
        using var db = _dbFactory.CreateDbContext();
        foreach (var def in definitions)
        {
            var existing = db.Settings.FirstOrDefault(s => s.Key == def.Key);
            if (existing != null)
            {
                existing.DisplayName = def.DisplayName;
                existing.Description = def.Description;
                existing.GroupName = def.GroupName;
                existing.GroupOrder = def.GroupOrder;
                existing.ItemOrder = def.ItemOrder;
                existing.SettingType = def.SettingType;
                existing.DefaultValue = def.DefaultValue;
                existing.PluginId = def.PluginId;
                if (def.Options != null)
                    existing.SetOptions(def.Options);
            }
            else
            {
                var item = new SettingItem
                {
                    Key = def.Key,
                    DisplayName = def.DisplayName,
                    Description = def.Description,
                    GroupName = def.GroupName,
                    GroupOrder = def.GroupOrder,
                    ItemOrder = def.ItemOrder,
                    SettingType = def.SettingType,
                    DefaultValue = def.DefaultValue,
                    PluginId = def.PluginId,
                    IsFolder = def.IsFolder,
                    RawValue = def.DefaultValue ?? string.Empty
                };
                if (def.Options != null)
                    item.SetOptions(def.Options);
                db.Settings.Add(item);
            }
        }
        db.SaveChanges();
        InvalidateCache();
    }

    public T? GetValue<T>(string key)
    {
        var cache = EnsureCache();
        return cache.TryGetValue(key, out var item) ? item.GetValue<T>() : default;
    }

    public string? GetValue(string key)
    {
        return GetValue<string>(key);
    }

    public void SetValue(string key, object? value)
    {
        using var db = _dbFactory.CreateDbContext();
        var item = db.Settings.FirstOrDefault(s => s.Key == key);
        if (item == null) return;
        item.SetValue(value);
        db.SaveChanges();

        var cache = EnsureCache();
        if (cache.TryGetValue(key, out var cached))
        {
            cached.SetValue(value);
        }
    }

    public List<SettingItem> GetAllSettings()
    {
        var cache = EnsureCache();
        return cache.Values.OrderBy(s => s.GroupOrder).ThenBy(s => s.ItemOrder).ToList();
    }

    public List<SettingItem> GetSettingsByGroup(string groupName)
    {
        var cache = EnsureCache();
        return cache.Values
            .Where(s => s.GroupName == groupName)
            .OrderBy(s => s.ItemOrder)
            .ToList();
    }

    public List<string> GetGroups()
    {
        var cache = EnsureCache();
        return cache.Values
            .Select(s => s.GroupName)
            .Distinct()
            .OrderBy(g => g)
            .ToList();
    }

    public SettingItem? GetSetting(string key)
    {
        var cache = EnsureCache();
        return cache.TryGetValue(key, out var item) ? item : null;
    }

    public void RemoveSetting(string key)
    {
        using var db = _dbFactory.CreateDbContext();
        var item = db.Settings.FirstOrDefault(s => s.Key == key);
        if (item == null) return;
        db.Settings.Remove(item);
        db.SaveChanges();
        InvalidateCache();
    }

    public void InitializeDefaults()
    {
        var themeDisplayName = _localizationService?.GetString("SETTING_THEME", "Theme") ?? "Theme";
        var themeDesc = _localizationService?.GetString("SETTING_THEME_DESC", "Select a theme for the application") ?? "Select a theme for the application";
        var sidebarDisplayName = _localizationService?.GetString("SETTING_COLLAPSE_SIDEBAR", "Collapse Sidebar") ?? "Collapse Sidebar";
        var sidebarDesc = _localizationService?.GetString("SETTING_COLLAPSE_SIDEBAR_DESC", "Collapse the sidebar navigation") ?? "Collapse the sidebar navigation";

        var appearanceGroup = _localizationService?.GetString("GROUP_APPEARANCE", "Appearance") ?? "Appearance";

        RegisterSetting(SettingDefinition.Dropdown("App.Theme", themeDisplayName, ["Default", "Light", "Dark"],
            themeDesc, appearanceGroup, 0, 0, "Default"));

        var langDisplayName = _localizationService?.GetString("SETTING_LANGUAGE", "Language") ?? "Language";
        var langDesc = _localizationService?.GetString("SETTING_LANGUAGE_DESC", "Select display language (restart required)") ?? "Select display language (restart required)";
        RegisterSetting(SettingDefinition.Dropdown("App.Locale", langDisplayName, ["Default", "zh-CN", "en-US"],
            langDesc, appearanceGroup, 0, 1, "Default"));

        RegisterSetting(SettingDefinition.Switch("App.SidebarCollapsed", sidebarDisplayName,
            sidebarDesc, appearanceGroup, 0, 2, false));
    }
}
