using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyTimePicker")]
[Menu("NAV_TimePicker", "KeyTimePicker", "NAV_DateTime")]
[ViewMap(typeof(TimePickerDemo))]
public partial class TimePickerDemoViewModel: ObservableValidator
{
    [ObservableProperty] private TimeSpan? _time;

    [ObservableProperty]
    [Required(ErrorMessage = "Please select a time")]
    private TimeSpan? _validatedTime;

    public TimePickerDemoViewModel()
    {
        Time = new TimeSpan(12, 20, 0);
        ValidatedTime = new TimeSpan(12, 20, 0);
    }
}
