using Avalonia.Controls;
using Avalonia.Plugin.ScottPlot.Controls;
using Avalonia.Plugin.ScottPlot.ViewModels;

namespace Avalonia.Plugin.ScottPlot.Pages;

public partial class SignalPlotDemo : UserControl
{
    public SignalPlotDemo()
    {
        InitializeComponent();
        var vm = new SignalPlotDemoViewModel();
        DataContext = vm;
        var plotView = this.FindControl<PlotView>("PlotView")!;
        plotView.Plot = vm.Plot;
        vm.PlotChanged += () => plotView.Refresh();
    }
}
