using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.ScottPlot.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SP = global::ScottPlot;

namespace LYBox.Plugin.ScottPlot.ViewModels;

[NavigationItem("KeyQuickStart")]
[Menu("NAV_QuickStart", "KeyQuickStart", "NAV_ScottPlot", IconName = "ChartMultiple")]
[ViewMap(typeof(QuickStartDemo))]
public partial class QuickStartDemoViewModel : ObservableObject
{
    private SP.Plot _plot;

    public event Action? PlotChanged;

    public QuickStartDemoViewModel()
    {
        _plot = new SP.Plot();
        _plot.Add.Signal(SP.Generate.Sin(51));
        _plot.Title("Sine Wave");
        _plot.XLabel("Sample");
        _plot.YLabel("Amplitude");
    }

    public SP.Plot Plot => _plot;

    [RelayCommand]
    private void AddSine()
    {
        _plot.Clear();
        _plot.Add.Signal(SP.Generate.Sin(51));
        _plot.Title("Sine Wave");
        _plot.Axes.AutoScale();
        PlotChanged?.Invoke();
    }

    [RelayCommand]
    private void AddCosine()
    {
        _plot.Clear();
        _plot.Add.Signal(SP.Generate.Cos(51));
        _plot.Title("Cosine Wave");
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
