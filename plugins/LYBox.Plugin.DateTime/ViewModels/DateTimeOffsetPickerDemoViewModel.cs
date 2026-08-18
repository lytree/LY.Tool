using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyDateTimeOffsetPicker")]
[Menu("NAV_DateTimeOffsetPicker", "KeyDateTimeOffsetPicker", "NAV_DateTime")]
[ViewMap(typeof(DateTimeOffsetPickerDemo))]
public partial class DateTimeOffsetPickerDemoViewModel : ObservableValidator
{
    [ObservableProperty] private System.DateTimeOffset? _selectedDateTime;

    [ObservableProperty]
    [Required(ErrorMessage = "Please select a date and time")]
    private System.DateTimeOffset? _validatedDateTime;

    public DateTimeOffsetPickerDemoViewModel()
    {
        SelectedDateTime = System.DateTimeOffset.Now;
        ValidatedDateTime = System.DateTimeOffset.Now;
    }
}
