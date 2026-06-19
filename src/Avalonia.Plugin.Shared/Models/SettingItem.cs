using System.Text.Json;

namespace Avalonia.Plugin.Shared.Models;

public class SettingItem
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string GroupName { get; set; } = "General";
    public int GroupOrder { get; set; }
    public int ItemOrder { get; set; }
    public SettingType SettingType { get; set; }
    public string RawValue { get; set; } = string.Empty;
    public string? OptionsJson { get; set; }
    public string? PluginId { get; set; }
    public string? DefaultValue { get; set; }

    public string? PlaceholderText { get; set; }

    public bool IsFolder { get; set; }

    public List<string> GetOptions()
    {
        if (string.IsNullOrEmpty(OptionsJson))
            return [];
        return JsonSerializer.Deserialize<List<string>>(OptionsJson) ?? [];
    }

    public void SetOptions(List<string> options)
    {
        OptionsJson = JsonSerializer.Serialize(options);
    }

    public T? GetValue<T>()
    {
        var value = RawValue;
        if (string.IsNullOrEmpty(value))
            value = DefaultValue;

        if (typeof(T) == typeof(bool))
            return (T)(object)(string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1");

        if (typeof(T) == typeof(int) && int.TryParse(value, out var intVal))
            return (T)(object)intVal;

        if (typeof(T) == typeof(double) && double.TryParse(value, out var dblVal))
            return (T)(object)dblVal;

        return (T?)(object?)value;
    }

    public void SetValue(object? value)
    {
        RawValue = value switch
        {
            bool b => b ? "true" : "false",
            _ => value?.ToString() ?? string.Empty
        };
    }
}
