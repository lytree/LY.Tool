using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyTimeRangePicker")]
[Menu("NAV_TimeRangePicker", "KeyTimeRangePicker", "NAV_DateTime")]
[ViewMap(typeof(TimeRangePickerDemo))]
public partial class TimeRangePickerDemoViewModel: ObservableValidator
{
    [ObservableProperty] private TimeSpan? _startTime;
    [ObservableProperty] private TimeSpan? _endTime;

    public ValidatedTimeRange ValidatedRange { get; } = new();

    public TimeRangePickerDemoViewModel()
    {
        StartTime = new TimeSpan(8, 21, 0);
        EndTime = new TimeSpan(18, 22, 0);
    }
}

public partial class ValidatedTimeRange : ObservableValidator
{
    [ObservableProperty]
    [Required(ErrorMessage = "Start time is required")]
    private TimeSpan? _start;

    [ObservableProperty]
    [Required(ErrorMessage = "End time is required")]
    private TimeSpan? _end;

    public ValidatedTimeRange()
    {
        Start = new TimeSpan(8, 21, 0);
        End = new TimeSpan(18, 22, 0);
    }
}
