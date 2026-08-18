using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyTimeOnlyPicker")]
[Menu("NAV_TimeOnlyPicker", "KeyTimeOnlyPicker", "NAV_DateTime")]
[ViewMap(typeof(TimeOnlyPickerDemo))]
public partial class TimeOnlyPickerDemoViewModel : ObservableValidator
{
    [ObservableProperty] private System.TimeOnly? _time;

    [ObservableProperty]
    [Required(ErrorMessage = "Please select a time")]
    private System.TimeOnly? _validatedTime;

    public TimeOnlyPickerDemoViewModel()
    {
        Time = new System.TimeOnly(12, 20, 0);
        ValidatedTime = new System.TimeOnly(12, 20, 0);
    }
}
