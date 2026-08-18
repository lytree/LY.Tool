using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.ButtonsInputs.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace LYBox.Plugin.ButtonsInputs.ViewModels;

[NavigationItem("EnumSelector")]
[Menu("NAV_EnumSelector", "EnumSelector", "NAV_ButtonsInputs")]
[ViewMap(typeof(EnumSelectorDemo))]
public partial class EnumSelectorDemoViewModel : ObservableObject
{
    [ObservableProperty] private Type? _selectedType;
    [ObservableProperty] private object? _value;
    [ObservableProperty] private object? _value2;
    [ObservableProperty] private object? _value3;

    public IList CustomEnumValues { get; set; } = new List<object>
    {
        DayOfWeek.Monday,
        DayOfWeek.Wednesday,
        DayOfWeek.Friday,
    };

    public ObservableCollection<Type?> Types { get; set; } =
    [
        typeof(HorizontalAlignment),
        typeof(VerticalAlignment),
        typeof(Orientation),
        typeof(Dock),
        typeof(GridResizeDirection),
        typeof(DayOfWeek),
        typeof(FillMode),
        typeof(IterationType),
        typeof(BindingMode),
        typeof(BindingPriority),
        typeof(StandardCursorType),
        typeof(Key),
        typeof(KeyModifiers),
        typeof(RoutingStrategies),
        typeof(CustomEnum)
    ];
}

public enum CustomEnum
{
    [Description("是")] Yes,
    [Description("否")] No,
}





