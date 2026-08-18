using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyDateOnlyRangePicker")]
[Menu("NAV_DateOnlyRangePicker", "KeyDateOnlyRangePicker", "NAV_DateTime")]
[ViewMap(typeof(DateOnlyRangePickerDemo))]
public partial class DateOnlyRangePickerDemoViewModel : ObservableValidator
{
    [ObservableProperty] private System.DateOnly? _startDate;
    [ObservableProperty] private System.DateOnly? _endDate;

    public ValidatedDateOnlyRange ValidatedRange { get; } = new();

    public DateOnlyRangePickerDemoViewModel()
    {
        StartDate = System.DateOnly.FromDateTime(System.DateTime.Today);
        EndDate = System.DateOnly.FromDateTime(System.DateTime.Today.AddDays(7));
    }
}

public partial class ValidatedDateOnlyRange : ObservableValidator
{
    [ObservableProperty]
    [Required(ErrorMessage = "Start date is required")]
    private System.DateOnly? _start;

    [ObservableProperty]
    [Required(ErrorMessage = "End date is required")]
    private System.DateOnly? _end;

    public ValidatedDateOnlyRange()
    {
        Start = System.DateOnly.FromDateTime(System.DateTime.Today);
        End = System.DateOnly.FromDateTime(System.DateTime.Today.AddDays(7));
    }
}
