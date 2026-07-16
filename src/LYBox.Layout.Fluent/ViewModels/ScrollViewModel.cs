using AvaloniaFluentUI.Locale;

namespace LYBox.Layout.Fluent.ViewModels;

public class ScrollViewModel : ViewModelBase
{
    public override string Title => LocalizationService.Instance.GetString("Scroll");
}
