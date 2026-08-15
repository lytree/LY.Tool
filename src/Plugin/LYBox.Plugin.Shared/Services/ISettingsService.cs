using LYBox.Plugin.Shared.Models;

namespace LYBox.Plugin.Shared.Services;

public interface ISettingsService
{
    void RegisterSetting(SettingDefinition definition);
    void RegisterSettings(IEnumerable<SettingDefinition> definitions);
    T? GetValue<T>(string key);
    string? GetValue(string key);
    void SetValue(string key, object? value);
    List<SettingItem> GetAllSettings();
    List<SettingItem> GetSettingsByGroup(string groupName);
    List<string> GetGroups();
    SettingItem? GetSetting(string key);
    void RemoveSetting(string key);
    void InitializeDefaults();
}
