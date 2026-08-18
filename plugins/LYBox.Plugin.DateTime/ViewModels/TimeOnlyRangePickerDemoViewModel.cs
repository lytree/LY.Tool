using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyTimeOnlyRangePicker")]
[Menu("NAV_TimeOnlyRangePicker", "KeyTimeOnlyRangePicker", "NAV_DateTime")]
[ViewMap(typeof(TimeOnlyRangePickerDemo))]
public partial class TimeOnlyRangePickerDemoViewModel : ObservableValidator
{
    [ObservableProperty] private System.TimeOnly? _startTime;
    [ObservableProperty] private System.TimeOnly? _endTime;

    public ValidatedTimeOnlyRange ValidatedRange { get; } = new();

    public TimeOnlyRangePickerDemoViewModel()
    {
        StartTime = new System.TimeOnly(8, 21, 0);
        EndTime = new System.TimeOnly(18, 22, 0);
    }
}

public partial class ValidatedTimeOnlyRange : ObservableValidator
{
    [ObservableProperty]
    [Required(ErrorMessage = "Start time is required")]
    private System.TimeOnly? _start;

    [ObservableProperty]
    [Required(ErrorMessage = "End time is required")]
    private System.TimeOnly? _end;

    public ValidatedTimeOnlyRange()
    {
        Start = new System.TimeOnly(8, 21, 0);
        End = new System.TimeOnly(18, 22, 0);
    }
}
