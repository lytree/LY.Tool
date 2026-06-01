using Avalonia.Controls;
using Avalonia.Plugin.ScottPlot.Controls;
using Avalonia.Plugin.ScottPlot.ViewModels;

namespace Avalonia.Plugin.ScottPlot.Pages;

public partial class QuickStartDemo : UserControl
{
    public QuickStartDemo()
    {
        InitializeComponent();
        var vm = new QuickStartDemoViewModel();
        DataContext = vm;
        var plotView = this.FindControl<PlotView>("PlotView")!;
        plotView.Plot = vm.Plot;
        vm.PlotChanged += () => plotView.Refresh();
    }
}
