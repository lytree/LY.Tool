using System.ComponentModel;
using AvaloniaFluentUI.Locale;

namespace LYBox.Layout.Fluent.ViewModels;

public partial class FrameViewPageViewModel : ViewModelBase
{
    public override string Title => LocalizationService.Instance.GetString("Frame");
}
