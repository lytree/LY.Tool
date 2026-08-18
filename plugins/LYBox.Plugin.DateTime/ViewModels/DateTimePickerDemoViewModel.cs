using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyDateTimePicker")]
[Menu("NAV_DateTimePicker", "KeyDateTimePicker", "NAV_DateTime")]
[ViewMap(typeof(DateTimePickerDemo))]
public partial class DateTimePickerDemoViewModel : ObservableValidator
{
    [ObservableProperty]
    [Required(ErrorMessage = "Please select a date and time")]
    private System.DateTime? _validatedDateTime;

    public DateTimePickerDemoViewModel()
    {
        ValidatedDateTime = System.DateTime.Now;
    }
}
