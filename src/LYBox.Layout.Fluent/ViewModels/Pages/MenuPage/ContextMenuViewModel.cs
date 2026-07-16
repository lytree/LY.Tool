using System.ComponentModel;
using AvaloniaFluentUI.Locale;

namespace LYBox.Layout.Fluent.ViewModels;

public partial class ContextMenuViewModel : ViewModelBase
{
    public override string Title => LocalizationService.Instance.GetString("ContextMenu");
}
