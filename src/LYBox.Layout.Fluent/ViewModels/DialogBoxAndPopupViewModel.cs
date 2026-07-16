using System.Collections.Generic;
using Avalonia.Controls;
using AvaloniaFluentUI.Controls;
using AvaloniaFluentUI.Locale;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LYBox.Layout.Fluent.Models;
using LYBox.Layout.Fluent.Services;

namespace LYBox.Layout.Fluent.ViewModels;

public partial class DialogBoxAndPopupViewModel : ViewModelBase
{
    public override string Title => LocalizationService.Instance.GetString("DialogAndPopup");
    
    public List<ButtonItemModel> DialogItemSource { get; }

    public DialogBoxAndPopupViewModel()
    {
        DialogItemSource = ButtonItemModel.CreateList(
            ("Flyout", "TaskDialog", "Dialog", "A task dialog."),
            ("Flyout", "Flyout", "Flyout", "Shows contextual information and enables user interaction."),
            ("ContentDialog", "ContentDialog", "Dialog", "A content dialog with mask."),
            ("TeachingTip", "TeachingTip", "Flyout", "A content-rich flyout for guiding users and enabling teaching moments.")
        );
    }
}
