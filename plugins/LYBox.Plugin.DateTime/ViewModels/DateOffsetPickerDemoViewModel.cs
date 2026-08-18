using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyDateOffsetPicker")]
[Menu("NAV_DateOffsetPicker", "KeyDateOffsetPicker", "NAV_DateTime")]
[ViewMap(typeof(DateOffsetPickerDemo))]
public partial class DateOffsetPickerDemoViewModel : ObservableValidator
{
    [ObservableProperty] private System.DateTimeOffset? _selectedDate;

    [ObservableProperty]
    [Required(ErrorMessage = "Please select a date")]
    private System.DateTimeOffset? _validatedDate;

    public DateOffsetPickerDemoViewModel()
    {
        SelectedDate = System.DateTimeOffset.Now;
        ValidatedDate = System.DateTimeOffset.Now;
    }
}
