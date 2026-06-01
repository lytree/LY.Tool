using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ScottPlot.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SP = global::ScottPlot;

namespace Avalonia.Plugin.ScottPlot.ViewModels;

[NavigationItem("KeySignalPlot")]
[Menu("NAV_SignalPlot", "KeySignalPlot", "NAV_ScottPlot")]
[ViewMap(typeof(SignalPlotDemo))]
public partial class SignalPlotDemoViewModel : ObservableObject
{
    private SP.Plot _plot;
    private static readonly Random _random = new();

    public event Action? PlotChanged;

    public SignalPlotDemoViewModel()
    {
        _plot = new SP.Plot();
        var data = SP.Generate.Sin(1000, 2);
        _plot.Add.Signal(data);
        _plot.Title("Signal Plot");
        _plot.XLabel("Sample Index");
        _plot.YLabel("Value");
    }

    public SP.Plot Plot => _plot;

    [RelayCommand]
    private void AddRandomSignal()
    {
        _plot.Clear();
        var data = new double[5000];
        for (int i = 0; i < data.Length; i++)
            data[i] = _random.NextDouble() * 2 - 1;
        _plot.Add.Signal(data);
        _plot.Title("Random Signal");
        _plot.Axes.AutoScale();
        PlotChanged?.Invoke();
    }

    [RelayCommand]
    private void AddSineSignal()
    {
        _plot.Clear();
        var data = SP.Generate.Sin(2000, 5);
        _plot.Add.Signal(data);
        _plot.Title("Sine Signal (5 Hz)");
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
