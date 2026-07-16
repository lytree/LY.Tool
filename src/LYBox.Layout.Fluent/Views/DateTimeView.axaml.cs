using System.Collections.Generic;
using AvaloniaFluentUI.Locale;
using LYBox.Layout.Fluent.Controls;

namespace LYBox.Layout.Fluent.Views;

public partial class DateTimeView : ViewBase
{
    public DateTimeView() : base("DateTime")
    {
        InitializeComponent();

        CodeCards = new Dictionary<string, CodeCard>()
        {
            { "CalendarDatePicker", CalendarDatePickerCard },
            { "DatePicker", DatePickerCard },
            { "TimePicker", TimePickerCard }
        };
    }
}
