using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avalonia.Plugin.Downloader.Models;

public partial class ScriptParameter : ObservableObject
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsRequired { get; init; }

    [ObservableProperty] private string? _defaultValue;

    public static ScriptParameter Text(string key, string displayName, string? description = null,
        string? defaultValue = null, bool required = false)
    {
        return new TextScriptParameter
        {
            Key = key,
            DisplayName = displayName,
            Description = description,
            DefaultValue = defaultValue,
            IsRequired = required
        };
    }

    public static ScriptParameter Switch(string key, string displayName, string? description = null,
        bool defaultValue = false)
    {
        return new BoolScriptParameter
        {
            Key = key,
            DisplayName = displayName,
            Description = description,
            DefaultValue = defaultValue ? "true" : "false"
        };
    }

    public static ScriptParameter Number(string key, string displayName, string? description = null,
        int defaultValue = 0)
    {
        return new NumberScriptParameter
        {
            Key = key,
            DisplayName = displayName,
            Description = description,
            DefaultValue = defaultValue.ToString()
        };
    }

    public static ScriptParameter Path(string key, string displayName, string? description = null,
        int defaultValue = 0)
    {
        return new PathScriptParameter
        {
            Key = key,
            DisplayName = displayName,
            Description = description,
            DefaultValue = defaultValue.ToString()
        };
    }
}

public partial class TextScriptParameter : ScriptParameter
{
}

public partial class BoolScriptParameter : ScriptParameter
{
    public bool DefaultBoolValue
    {
        get => DefaultValue?.ToLower() == "true";
        set => DefaultValue = value ? "true" : "false";
    }
}

public partial class NumberScriptParameter : ScriptParameter
{
    public int DefaultIntValue
    {
        get => int.TryParse(DefaultValue, out var v) ? v : 0;
        set => DefaultValue = value.ToString();
    }
}

public partial class PathScriptParameter : ScriptParameter
{
    [RelayCommand]
    private async Task Browse()
    {
        var topLevel = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (topLevel == null) return;

        var storageProvider = topLevel.StorageProvider;

        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = $"选择{DisplayName}",
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            DefaultValue = result[0].TryGetLocalPath() ?? result[0].Path.ToString();
        }
    }
}

public class ScriptDescriptor
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<ScriptParameter> Parameters { get; init; } = [];
}
