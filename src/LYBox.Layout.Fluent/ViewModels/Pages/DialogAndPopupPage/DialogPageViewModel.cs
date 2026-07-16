using AvaloniaFluentUI.Locale;

namespace LYBox.Layout.Fluent.ViewModels;

public partial class DialogPageViewModel : ViewModelBase
{
    public override string Title => LocalizationService.Instance.GetString("Dialog");
}
