using Avalonia.Controls;
using Avalonia.Plugin.ScottPlot.Controls;
using Avalonia.Plugin.ScottPlot.ViewModels;

namespace Avalonia.Plugin.ScottPlot.Pages;

public partial class BarChartDemo : UserControl
{
    public BarChartDemo()
    {
        InitializeComponent();
        var vm = new BarChartDemoViewModel();
        DataContext = vm;
        var plotView = this.FindControl<PlotView>("PlotView")!;
        plotView.Plot = vm.Plot;
        vm.PlotChanged += () => plotView.Refresh();
    }
}
