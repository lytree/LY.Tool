using AvaloniaFluentUI.Locale;

namespace LYBox.Layout.Fluent.ViewModels;

public partial class ShortcutKeyPickerPageViewModel : ViewModelBase
{
    public override string Title => LocalizationService.Instance.GetString("ShortcutKeyPicker");
    public string[] Keys => ["Ctrl", "Shift", "Alt", "D"];
}
