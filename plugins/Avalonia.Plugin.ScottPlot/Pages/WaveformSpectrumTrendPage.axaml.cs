using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Plugin.ScottPlot.Controls;
using Avalonia.Plugin.ScottPlot.ViewModels;

namespace Avalonia.Plugin.ScottPlot.Pages;

public partial class WaveformSpectrumTrendPage : UserControl
{
    private WaveformSpectrumTrendViewModel _vm = null!;
    private PlotView _waveformPlotView = null!;
    private PlotView _spectrumPlotView = null!;
    private PlotView _trendPlotView = null!;

    public WaveformSpectrumTrendPage()
    {
        InitializeComponent();
        _vm = new WaveformSpectrumTrendViewModel();
        DataContext = _vm;

        _waveformPlotView = this.FindControl<PlotView>("WaveformPlotView")!;
        _spectrumPlotView = this.FindControl<PlotView>("SpectrumPlotView")!;
        _trendPlotView = this.FindControl<PlotView>("TrendPlotView")!;

        _waveformPlotView.Plot = _vm.WaveformPlot;
        _spectrumPlotView.Plot = _vm.SpectrumPlot;
        _trendPlotView.Plot = _vm.TrendPlot;

        _vm.WaveformPlotChanged += () => _waveformPlotView.Refresh();
        _vm.SpectrumPlotChanged += () => _spectrumPlotView.Refresh();
        _vm.TrendPlotChanged += () => _trendPlotView.Refresh();

        // 使用 PointerReleased 避免与 PlotView 内置交互冲突
        _trendPlotView.PointerReleased += OnTrendPlotPointerReleased;
    }

    private void OnTrendPlotPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            var props = e.GetCurrentPoint(_trendPlotView).Properties;
            if (props.PointerUpdateKind != PointerUpdateKind.LeftButtonReleased) return;

            var position = e.GetPosition(_trendPlotView);
            var plot = _vm.TrendPlot;

            // 获取坐标轴范围
            double xMin = plot.Axes.Bottom.Min;
            double xMax = plot.Axes.Bottom.Max;
            double yMin = plot.Axes.Left.Min;
            double yMax = plot.Axes.Left.Max;

            if (xMax <= xMin) return;

            // 将像素坐标映射到数据坐标
            double displayScale = _trendPlotView.DisplayScale;
            if (displayScale <= 0) displayScale = 1f;

            double pixelX = position.X * displayScale;
            double pixelY = position.Y * displayScale;
            double plotWidth = _trendPlotView.Bounds.Width * displayScale;
            double plotHeight = _trendPlotView.Bounds.Height * displayScale;

            if (plotWidth <= 0 || plotHeight <= 0) return;

            double dataX = xMin + (pixelX / plotWidth) * (xMax - xMin);

            // 找到最近的数据点索引
            int nearestIndex = (int)Math.Round(dataX);
            if (nearestIndex >= 0 && nearestIndex < 50)
            {
                _vm.OnTrendPointClicked(nearestIndex);
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[ScottPlot] 趋势图点击坐标转换失败: {ex.Message}"); }
    }
}
