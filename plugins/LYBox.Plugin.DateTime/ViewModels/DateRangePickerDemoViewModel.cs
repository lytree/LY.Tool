using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyDateRangePicker")]
[Menu("NAV_DateRangePicker", "KeyDateRangePicker", "NAV_DateTime")]
[ViewMap(typeof(DateRangePickerDemo))]
public partial class DateRangePickerDemoViewModel: ObservableValidator
{
    [ObservableProperty] private System.DateTime? _startDate;
    [ObservableProperty] private System.DateTime? _endDate;

    public ValidatedDateRange ValidatedRange { get; } = new();

    public DateRangePickerDemoViewModel()
    {
        StartDate = System.DateTime.Today;
        EndDate = System.DateTime.Today.AddDays(7);
    }
}

public partial class ValidatedDateRange : ObservableValidator
{
    [ObservableProperty]
    [Required(ErrorMessage = "Start date is required")]
    private System.DateTime? _start;

    [ObservableProperty]
    [Required(ErrorMessage = "End date is required")]
    private System.DateTime? _end;

    public ValidatedDateRange()
    {
        Start = System.DateTime.Today;
        End = System.DateTime.Today.AddDays(7);
    }
}
