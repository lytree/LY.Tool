namespace LYBox.Plugin.Shared.Models;

public class SettingDefinition
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string GroupName { get; set; } = "General";
    public int GroupOrder { get; set; }
    public int ItemOrder { get; set; }
    public SettingType SettingType { get; set; }
    public string? DefaultValue { get; set; }
    public List<string>? Options { get; set; }

    public string? PlaceholderText { get; set; }

    public string? PluginId { get; set; }

    public bool IsFolder { get; set; }

    public static SettingDefinition Text(string key, string displayName, string? description = null,string placeholder = "",
        string group = "General", int groupOrder = 0, int itemOrder = 0, string? defaultValue = null, string? pluginId = null)
    {
        return new SettingDefinition
        {
            Key = key,
            DisplayName = displayName,
            Description = description,
            GroupName = group,
            GroupOrder = groupOrder,
            ItemOrder = itemOrder,
            SettingType = SettingType.Text,
            DefaultValue = defaultValue,
            PlaceholderText = placeholder,
            PluginId = pluginId
        };
    }

    public static SettingDefinition Switch(string key, string displayName, string? description = null,
        string group = "General", int groupOrder = 0, int itemOrder = 0, bool defaultValue = false, string? pluginId = null)
    {
        return new SettingDefinition
        {
            Key = key,
            DisplayName = displayName,
            Description = description,
            GroupName = group,
            GroupOrder = groupOrder,
            ItemOrder = itemOrder,
            SettingType = SettingType.Switch,
            DefaultValue = defaultValue ? "true" : "false",
            PluginId = pluginId
        };
    }

    public static SettingDefinition Dropdown(string key, string displayName, List<string> options, string? description = null,
        string group = "General", int groupOrder = 0, int itemOrder = 0, string? defaultValue = null, string? pluginId = null)
    {
        return new SettingDefinition
        {
            Key = key,
            DisplayName = displayName,
            Description = description,
            GroupName = group,
            GroupOrder = groupOrder,
            ItemOrder = itemOrder,
            SettingType = SettingType.Dropdown,
            Options = options,
            DefaultValue = defaultValue,
            PluginId = pluginId
        };
    }

    public static SettingDefinition Path(string key, string displayName, string? description = null,
        string group = "General", int groupOrder = 0, int itemOrder = 0, string? defaultValue = null, string? pluginId = null,
        bool isFolder = false)
    {
        return new SettingDefinition
        {
            Key = key,
            DisplayName = displayName,
            Description = description,
            GroupName = group,
            GroupOrder = groupOrder,
            ItemOrder = itemOrder,
            SettingType = SettingType.Path,
            DefaultValue = defaultValue,
            PluginId = pluginId,
            IsFolder = isFolder
        };
    }
}
