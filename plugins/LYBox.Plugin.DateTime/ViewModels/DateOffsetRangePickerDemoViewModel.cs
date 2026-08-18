using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyDateOffsetRangePicker")]
[Menu("NAV_DateOffsetRangePicker", "KeyDateOffsetRangePicker", "NAV_DateTime")]
[ViewMap(typeof(DateOffsetRangePickerDemo))]
public partial class DateOffsetRangePickerDemoViewModel : ObservableValidator
{
    [ObservableProperty] private System.DateTimeOffset? _startDate;
    [ObservableProperty] private System.DateTimeOffset? _endDate;

    public ValidatedDateTimeOffsetRange ValidatedRange { get; } = new();

    public DateOffsetRangePickerDemoViewModel()
    {
        StartDate = System.DateTimeOffset.Now;
        EndDate = System.DateTimeOffset.Now.AddDays(7);
    }
}

public partial class ValidatedDateTimeOffsetRange : ObservableValidator
{
    [ObservableProperty]
    [Required(ErrorMessage = "Start date is required")]
    private System.DateTimeOffset? _start;

    [ObservableProperty]
    [Required(ErrorMessage = "End date is required")]
    private System.DateTimeOffset? _end;

    public ValidatedDateTimeOffsetRange()
    {
        Start = System.DateTimeOffset.Now;
        End = System.DateTimeOffset.Now.AddDays(7);
    }
}
