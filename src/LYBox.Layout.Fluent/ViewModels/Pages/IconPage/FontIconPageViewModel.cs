using AvaloniaFluentUI.Locale;

namespace LYBox.Layout.Fluent.ViewModels;

public partial class FontIconPageViewModel : ViewModelBase
{
    public override string Title => LocalizationService.Instance.GetString("FontIcon");
}
