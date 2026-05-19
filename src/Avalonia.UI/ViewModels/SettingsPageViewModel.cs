using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Models;
using Avalonia.Plugin.Shared.Services;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.UI.Theme;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avalonia.UI.ViewModels;

public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly ISettingsService? _settingsService;

    public ObservableCollection<SettingsGroupViewModel> Groups { get; } = [];

    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string _saveStatusText = string.Empty;

    public SettingsPageViewModel()
    {
    }

    public SettingsPageViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadSettings();
    }

    [RelayCommand]
    public void Refresh()
    {
        LoadSettings();
    }

    [RelayCommand]
    public void Save()
    {
        if (_settingsService == null) return;

        try
        {
            foreach (var group in Groups)
            {
                foreach (var entry in group.Items.Where(e => e.IsDirty))
                {
                    _settingsService.SetValue(entry.Key, entry.GetCurrentValue());
                    entry.MarkSaved();
                }
            }

            IsDirty = false;
            SaveStatusText = "已保存";

            ApplyRuntimeSettings();
        }
        catch (Exception)
        {
            SaveStatusText = "保存失败";
        }
    }

    [RelayCommand]
    public void Reset()
    {
        if (_settingsService == null) return;

        foreach (var group in Groups)
        {
            foreach (var entry in group.Items)
            {
                entry.ResetToSaved();
            }
        }

        IsDirty = false;
        SaveStatusText = string.Empty;
    }

    private void ApplyRuntimeSettings()
    {
        if (_settingsService == null) return;

        var theme = _settingsService.GetValue("App.Theme");
        if (theme != null)
        {
            var app = Application.Current;
            if (app is not null)
            {
                app.RequestedThemeVariant = theme switch
                {
                    "Light" => ThemeVariant.Light,
                    "Dark" => ThemeVariant.Dark,
                    _ => ThemeVariant.Default
                };
            }
        }

        var locale = _settingsService.GetValue("App.Locale");
        if (!string.IsNullOrEmpty(locale))
        {
            try
            {
                var culture = new CultureInfo(locale);
                var app = Application.Current;
                if (app is not null)
                {
                    UrsaSemiTheme.OverrideLocaleResources(app, culture);
                }
            }
            catch (CultureNotFoundException) { }
        }
    }

    internal void MarkDirty()
    {
        IsDirty = true;
        SaveStatusText = string.Empty;
    }

    private void LoadSettings()
    {
        if (_settingsService == null) return;

        Groups.Clear();
        IsDirty = false;
        SaveStatusText = string.Empty;

        var allSettings = _settingsService.GetAllSettings();
        var grouped = allSettings.GroupBy(s => s.GroupName);

        foreach (var group in grouped.OrderBy(g => g.Min(s => s.GroupOrder)))
        {
            var groupVm = new SettingsGroupViewModel(group.Key, _settingsService, this);
            foreach (var setting in group.OrderBy(s => s.ItemOrder))
            {
                groupVm.Items.Add(CreateEntry(setting, _settingsService, this));
            }
            Groups.Add(groupVm);
        }
    }

    private static SettingEntryViewModel CreateEntry(SettingItem setting, ISettingsService settingsService, SettingsPageViewModel parent)
    {
        return setting.SettingType switch
        {
            SettingType.Text => new TextSettingEntryViewModel(setting, settingsService, parent),
            SettingType.Switch => new SwitchSettingEntryViewModel(setting, settingsService, parent),
            SettingType.Dropdown => new DropdownSettingEntryViewModel(setting, settingsService, parent),
            SettingType.Path => new PathSettingEntryViewModel(setting, settingsService, parent),
            _ => new TextSettingEntryViewModel(setting, settingsService, parent)
        };
    }
}

public class SettingsGroupViewModel : ViewModelBase
{
    public string GroupName { get; }
    public ObservableCollection<SettingEntryViewModel> Items { get; } = [];
    private readonly ISettingsService _settingsService;

    public SettingsGroupViewModel(string groupName, ISettingsService settingsService, SettingsPageViewModel parent)
    {
        GroupName = groupName;
        _settingsService = settingsService;
    }
}

public abstract partial class SettingEntryViewModel : ViewModelBase
{
    protected readonly ISettingsService SettingsService;
    protected readonly SettingItem Setting;
    protected readonly SettingsPageViewModel Parent;

    public string Key => Setting.Key;
    public string DisplayName => Setting.DisplayName;
    public string? Description => Setting.Description;

    [ObservableProperty] private bool _isDirty;

    public SettingEntryViewModel(SettingItem setting, ISettingsService settingsService, SettingsPageViewModel parent)
    {
        Setting = setting;
        SettingsService = settingsService;
        Parent = parent;
    }

    public abstract object? GetCurrentValue();
    public abstract void ResetToSaved();
    public abstract void MarkSaved();

    protected void OnValueChanged()
    {
        IsDirty = true;
        Parent.MarkDirty();
    }
}

public partial class TextSettingEntryViewModel : SettingEntryViewModel
{
    public string PlaceholderText => Setting.PlaceholderText ?? "";

    [ObservableProperty] private string _textValue;
    private string _savedValue;

    public TextSettingEntryViewModel(SettingItem setting, ISettingsService settingsService, SettingsPageViewModel parent)
        : base(setting, settingsService, parent)
    {
        _textValue = setting.RawValue;
        _savedValue = setting.RawValue;
    }

    public override object? GetCurrentValue() => TextValue;

    public override void ResetToSaved()
    {
        TextValue = _savedValue;
        IsDirty = false;
    }

    public override void MarkSaved()
    {
        _savedValue = TextValue;
        IsDirty = false;
    }

    partial void OnTextValueChanged(string value) => OnValueChanged();
}

public partial class SwitchSettingEntryViewModel : SettingEntryViewModel
{
    [ObservableProperty] private bool _switchValue;
    private bool _savedValue;

    public SwitchSettingEntryViewModel(SettingItem setting, ISettingsService settingsService, SettingsPageViewModel parent)
        : base(setting, settingsService, parent)
    {
        _switchValue = setting.GetValue<bool>();
        _savedValue = _switchValue;
    }

    public override object? GetCurrentValue() => SwitchValue;

    public override void ResetToSaved()
    {
        SwitchValue = _savedValue;
        IsDirty = false;
    }

    public override void MarkSaved()
    {
        _savedValue = SwitchValue;
        IsDirty = false;
    }

    partial void OnSwitchValueChanged(bool value) => OnValueChanged();
}

public partial class DropdownSettingEntryViewModel : SettingEntryViewModel
{
    [ObservableProperty] private string? _dropdownValue;
    public ObservableCollection<string> DropdownOptions { get; }
    private string? _savedValue;

    public DropdownSettingEntryViewModel(SettingItem setting, ISettingsService settingsService, SettingsPageViewModel parent)
        : base(setting, settingsService, parent)
    {
        _dropdownValue = setting.RawValue;
        _savedValue = setting.RawValue;
        DropdownOptions = new ObservableCollection<string>(setting.GetOptions());
    }

    public override object? GetCurrentValue() => DropdownValue;

    public override void ResetToSaved()
    {
        DropdownValue = _savedValue;
        IsDirty = false;
    }

    public override void MarkSaved()
    {
        _savedValue = DropdownValue;
        IsDirty = false;
    }

    partial void OnDropdownValueChanged(string? value) => OnValueChanged();
}

public partial class PathSettingEntryViewModel : SettingEntryViewModel
{
    public string PlaceholderText => Setting.PlaceholderText ?? "选择文件路径...";

    [ObservableProperty] private string _pathValue;
    private string _savedValue;

    public PathSettingEntryViewModel(SettingItem setting, ISettingsService settingsService, SettingsPageViewModel parent)
        : base(setting, settingsService, parent)
    {
        _pathValue = setting.RawValue;
        _savedValue = setting.RawValue;
    }

    public override object? GetCurrentValue() => PathValue;

    public override void ResetToSaved()
    {
        PathValue = _savedValue;
        IsDirty = false;
    }

    public override void MarkSaved()
    {
        _savedValue = PathValue;
        IsDirty = false;
    }

    partial void OnPathValueChanged(string value) => OnValueChanged();

    [RelayCommand]
    private async Task Browse()
    {
        var topLevel = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (topLevel == null) return;

        var storageProvider = topLevel.StorageProvider;

        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"选择{DisplayName}",
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            PathValue = result[0].TryGetLocalPath() ?? result[0].Path.ToString();
        }
    }
}
