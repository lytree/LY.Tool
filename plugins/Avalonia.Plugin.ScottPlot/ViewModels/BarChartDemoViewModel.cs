using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ScottPlot.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SP = global::ScottPlot;

namespace Avalonia.Plugin.ScottPlot.ViewModels;

[NavigationItem("KeyBarChart")]
[Menu("NAV_BarChart", "KeyBarChart", "NAV_ScottPlot")]
[ViewMap(typeof(BarChartDemo))]
public partial class BarChartDemoViewModel : ObservableObject
{
    private SP.Plot _plot;
    private static readonly Random _random = new();

    public event Action? PlotChanged;

    public BarChartDemoViewModel()
    {
        _plot = new SP.Plot();
        double[] values = [5, 15, 10, 20, 8, 12];
        _plot.Add.Bars(values);
        _plot.Title("Bar Chart");
        _plot.Axes.AutoScale();
    }

    public SP.Plot Plot => _plot;

    [RelayCommand]
    private void AddRandomBars()
    {
        _plot.Clear();
        double[] values = new double[8];
        for (int i = 0; i < values.Length; i++)
            values[i] = _random.Next(5, 30);
        _plot.Add.Bars(values);
        _plot.Title("Random Bars");
        _plot.Axes.AutoScale();
        PlotChanged?.Invoke();
    }

    [RelayCommand]
    private void AddCategoryBars()
    {
        _plot.Clear();
        string[] categories = ["Apple", "Banana", "Cherry", "Date", "Elderberry"];
        double[] values = [12, 8, 15, 6, 10];
        var bars = _plot.Add.Bars(values);
        for (int i = 0; i < categories.Length; i++)
        {
            bars.Bars[i].Label = categories[i];
        }
        _plot.Title("Category Bars");
        _plot.Axes.AutoScale();
        PlotChanged?.Invoke();
    }

    [RelayCommand]
    private void Clear()
    {
        _plot.Clear();
        _plot.Title("Cleared");
        PlotChanged?.Invoke();
    }
}
