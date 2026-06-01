using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ScottPlot.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SP = global::ScottPlot;

namespace Avalonia.Plugin.ScottPlot.ViewModels;

[NavigationItem("KeyScatterPlot")]
[Menu("NAV_ScatterPlot", "KeyScatterPlot", "NAV_ScottPlot")]
[ViewMap(typeof(ScatterPlotDemo))]
public partial class ScatterPlotDemoViewModel : ObservableObject
{
    private SP.Plot _plot;
    private static readonly Random _random = new();

    public event Action? PlotChanged;

    public ScatterPlotDemoViewModel()
    {
        _plot = new SP.Plot();
        var xs = SP.Generate.Consecutive(50);
        var ys = SP.Generate.Sin(50);
        _plot.Add.Scatter(xs, ys);
        _plot.Title("Scatter Plot");
        _plot.XLabel("X");
        _plot.YLabel("Y");
    }

    public SP.Plot Plot => _plot;

    [RelayCommand]
    private void AddRandomPoints()
    {
        _plot.Clear();
        int count = 100;
        double[] xs = new double[count];
        double[] ys = new double[count];
        for (int i = 0; i < count; i++)
        {
            xs[i] = _random.NextDouble() * 100;
            ys[i] = _random.NextDouble() * 100;
        }
        _plot.Add.Scatter(xs, ys);
        _plot.Title("Random Points");
        _plot.Axes.AutoScale();
        PlotChanged?.Invoke();
    }

    [RelayCommand]
    private void AddSpiral()
    {
        _plot.Clear();
        int count = 200;
        double[] xs = new double[count];
        double[] ys = new double[count];
        for (int i = 0; i < count; i++)
        {
            double angle = i * 0.1;
            double radius = i * 0.05;
            xs[i] = radius * Math.Cos(angle);
            ys[i] = radius * Math.Sin(angle);
        }
        _plot.Add.Scatter(xs, ys);
        _plot.Title("Spiral");
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
