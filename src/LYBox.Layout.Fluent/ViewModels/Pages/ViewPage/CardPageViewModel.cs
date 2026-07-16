using System.ComponentModel;
using AvaloniaFluentUI.Locale;

namespace LYBox.Layout.Fluent.ViewModels;

public partial class CardPageViewModel : ViewModelBase
{
    public override string Title => LocalizationService.Instance.GetString("Card");
}
