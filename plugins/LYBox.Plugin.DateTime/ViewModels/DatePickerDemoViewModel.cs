using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyDatePicker")]
[Menu("NAV_DatePicker", "KeyDatePicker", "NAV_DateTime")]
[ViewMap(typeof(DatePickerDemo))]
public partial class DatePickerDemoViewModel : ObservableValidator
{
    [ObservableProperty] private System.DateTime? _selectedDate;

    [ObservableProperty]
    [Required(ErrorMessage = "Please select a date")]
    private System.DateTime? _validatedDate;

    public DatePickerDemoViewModel()
    {
        SelectedDate = System.DateTime.Today;
        ValidatedDate = System.DateTime.Today;
    }
}





