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

    public SettingsService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        _localizationService = ServiceLocator.GetService<ILocalizationService>();
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
            RawValue = definition.DefaultValue ?? string.Empty
        };
        if (definition.Options != null)
            item.SetOptions(definition.Options);

        db.Settings.Add(item);
        db.SaveChanges();
    }

    public void RegisterSettings(IEnumerable<SettingDefinition> definitions)
    {
        foreach (var def in definitions)
        {
            RegisterSetting(def);
        }
    }

    public T? GetValue<T>(string key)
    {
        var item = GetSetting(key);
        return item != null ? item.GetValue<T>() : default;
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
    }

    public List<SettingItem> GetAllSettings()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Settings.OrderBy(s => s.GroupOrder).ThenBy(s => s.ItemOrder).ToList();
    }

    public List<SettingItem> GetSettingsByGroup(string groupName)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Settings
            .Where(s => s.GroupName == groupName)
            .OrderBy(s => s.ItemOrder)
            .ToList();
    }

    public List<string> GetGroups()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Settings
            .Select(s => s.GroupName)
            .Distinct()
            .OrderBy(g => g)
            .ToList();
    }

    public SettingItem? GetSetting(string key)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Settings.FirstOrDefault(s => s.Key == key);
    }

    public void RemoveSetting(string key)
    {
        using var db = _dbFactory.CreateDbContext();
        var item = db.Settings.FirstOrDefault(s => s.Key == key);
        if (item == null) return;
        db.Settings.Remove(item);
        db.SaveChanges();
    }

    public void InitializeDefaults()
    {
        var themeDisplayName = _localizationService?.GetString("SETTING_THEME", "Theme") ?? "Theme";
        var themeDesc = _localizationService?.GetString("SETTING_THEME_DESC", "Select a theme for the application") ?? "Select a theme for the application";
        var langDisplayName = _localizationService?.GetString("SETTING_LANGUAGE", "Language") ?? "Language";
        var langDesc = _localizationService?.GetString("SETTING_LANGUAGE_DESC", "Select display language (restart required for full effect)") ?? "Select display language (restart required for full effect)";
        var sidebarDisplayName = _localizationService?.GetString("SETTING_COLLAPSE_SIDEBAR", "Collapse Sidebar") ?? "Collapse Sidebar";
        var sidebarDesc = _localizationService?.GetString("SETTING_COLLAPSE_SIDEBAR_DESC", "Collapse the sidebar navigation") ?? "Collapse the sidebar navigation";
        var userNameDisplayName = _localizationService?.GetString("SETTING_USER_NAME", "User Name") ?? "User Name";
        var userNameDesc = _localizationService?.GetString("SETTING_USER_NAME_DESC", "Set your display name") ?? "Set your display name";

        var appearanceGroup = _localizationService?.GetString("GROUP_APPEARANCE", "Appearance") ?? "Appearance";
        var generalGroup = _localizationService?.GetString("GROUP_GENERAL", "General") ?? "General";

        RegisterSetting(SettingDefinition.Dropdown("App.Theme", themeDisplayName, ["Default", "Light", "Dark"],
            themeDesc, appearanceGroup, 0, 0, "Default"));

        RegisterSetting(SettingDefinition.Dropdown("App.Locale", langDisplayName, ["en-US", "zh-CN"],
            langDesc, appearanceGroup, 0, 1, "en-US"));

        RegisterSetting(SettingDefinition.Switch("App.SidebarCollapsed", sidebarDisplayName,
            sidebarDesc, appearanceGroup, 0, 2, false));

        RegisterSetting(SettingDefinition.Text("App.UserName", userNameDisplayName,
            userNameDesc, generalGroup, "", 1, 0, ""));
    }
}
