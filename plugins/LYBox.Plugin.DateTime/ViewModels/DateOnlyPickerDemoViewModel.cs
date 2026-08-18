using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyDateOnlyPicker")]
[Menu("NAV_DateOnlyPicker", "KeyDateOnlyPicker", "NAV_DateTime")]
[ViewMap(typeof(DateOnlyPickerDemo))]
public partial class DateOnlyPickerDemoViewModel : ObservableValidator
{
    [ObservableProperty] private System.DateOnly? _selectedDate;

    [ObservableProperty]
    [Required(ErrorMessage = "Please select a date")]
    private System.DateOnly? _validatedDate;

    public DateOnlyPickerDemoViewModel()
    {
        SelectedDate = System.DateOnly.FromDateTime(System.DateTime.Today);
        ValidatedDate = System.DateOnly.FromDateTime(System.DateTime.Today);
    }
}
