using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using ScottPlot;

namespace Avalonia.Plugin.Shared.Controls;

public class PlotView : global::Avalonia.Controls.Control, IPlotControl
{
    public static readonly StyledProperty<ScottPlot.Plot?> PlotProperty =
        AvaloniaProperty.Register<PlotView, ScottPlot.Plot?>(nameof(Plot));

    public ScottPlot.Plot Plot
    {
        get => GetValue(PlotProperty) ?? _internalPlot;
        set => SetValue(PlotProperty, value);
    }

    public IMultiplot Multiplot { get; set; }
    public IPlotMenu? Menu { get; set; }
    public ScottPlot.Interactivity.UserInputProcessor UserInputProcessor { get; }
    public GRContext? GRContext => null;
    public float DisplayScale { get; set; }

    private ScottPlot.Plot _internalPlot;

    public PlotView()
    {
        _internalPlot = new() { PlotControl = this };
        Multiplot = new Multiplot(_internalPlot);
        Plot = _internalPlot;

        ClipToBounds = true;
        DisplayScale = DetectDisplayScale();
        UserInputProcessor = new(this);
        Menu = new PlotMenu(this);
        Focusable = true;

        Refresh();
    }

    static PlotView()
    {
        PlotProperty.Changed.AddClassHandler<PlotView>((x, e) => x.OnPlotChanged(e));
    }

    private void OnPlotChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is ScottPlot.Plot newPlot)
        {
            newPlot.PlotControl = this;
            Multiplot.Reset(newPlot);
            _internalPlot = newPlot;
        }
    }

    private class CustomDrawOp : ICustomDrawOperation
    {
        private readonly IMultiplot _multiplot;
        private readonly float _displayScale;

        public Rect Bounds { get; }
        public bool HitTest(Point p) => true;
        public bool Equals(ICustomDrawOperation? other) => false;

        public CustomDrawOp(Rect bounds, IMultiplot multiplot, float displayScale)
        {
            _multiplot = multiplot;
            _displayScale = displayScale;
            Bounds = bounds;
        }

        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null) return;

            using var lease = leaseFeature.Lease();
            ScottPlot.PixelRect rect = new(0, (float)Bounds.Width * _displayScale,
                (float)Bounds.Height * _displayScale, 0);

            using SKAutoCanvasRestore _ = new(lease.SkCanvas, false);
            lease.SkCanvas.SaveLayer();
            lease.SkCanvas.Scale(_displayScale);
            _multiplot.Render(lease.SkCanvas, rect);
        }
    }

    public override void Render(DrawingContext context)
    {
        Rect controlBounds = new(Bounds.Size);
        CustomDrawOp customDrawOp = new(controlBounds, Multiplot, DisplayScale);
        context.Custom(customDrawOp);
    }

    public void Reset()
    {
        ScottPlot.Plot plot = new() { PlotControl = this };
        Reset(plot);
    }

    public void Reset(ScottPlot.Plot plot)
    {
        ScottPlot.Plot oldPlot = _internalPlot;
        _internalPlot = plot;
        Plot = plot;
        oldPlot?.Dispose();
        Multiplot.Reset(plot);
    }

    public void Refresh()
    {
        Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
    }

    public void ShowContextMenu(Pixel position)
    {
        Menu?.ShowContextMenu(position);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        Pixel pixel = e.ToPixel(this);
        PointerUpdateKind kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
        UserInputProcessor.ProcessMouseDown(pixel, kind);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        Pixel pixel = e.ToPixel(this);
        PointerUpdateKind kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
        UserInputProcessor.ProcessMouseUp(pixel, kind);
        e.Pointer.Capture(null);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        Pixel pixel = e.ToPixel(this);
        UserInputProcessor.ProcessMouseMove(pixel);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        Pixel pixel = e.ToPixel(this);
        float delta = (float)e.Delta.Y;
        if (delta != 0)
        {
            UserInputProcessor.ProcessMouseWheel(pixel, delta);
        }

        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        UserInputProcessor.ProcessKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        UserInputProcessor.ProcessKeyUp(e);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        UserInputProcessor.ProcessLostFocus();
    }

    public float DetectDisplayScale()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not null)
        {
            var scaling = topLevel.RenderScaling;
            if (scaling > 0 && scaling < 10) return (float)scaling;
        }

        return 1.0f;
    }

    public void SetCursor(ScottPlot.Cursor cursor)
    {
        Cursor = cursor.GetCursor();
    }
}
