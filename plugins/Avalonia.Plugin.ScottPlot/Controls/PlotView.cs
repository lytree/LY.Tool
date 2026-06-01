using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using SP = global::ScottPlot;

namespace Avalonia.Plugin.ScottPlot.Controls;

public class PlotView : global::Avalonia.Controls.Control, SP.IPlotControl
{
    public static readonly StyledProperty<SP.Plot?> PlotProperty =
        AvaloniaProperty.Register<PlotView, SP.Plot?>(nameof(Plot));

    public SP.Plot Plot
    {
        get => GetValue(PlotProperty) ?? _internalPlot;
        set => SetValue(PlotProperty, value);
    }

    public SP.IMultiplot Multiplot { get; set; }
    public SP.IPlotMenu? Menu { get; set; }
    public SP.Interactivity.UserInputProcessor UserInputProcessor { get; }
    public GRContext? GRContext => null;
    public float DisplayScale { get; set; }

    private SP.Plot _internalPlot;
    private SP.Multiplot _multiplotImpl;

    public PlotView()
    {
        _internalPlot = new() { PlotControl = this };
        _multiplotImpl = new SP.Multiplot(_internalPlot);
        Multiplot = _multiplotImpl;
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
        if (e.NewValue is SP.Plot newPlot)
        {
            newPlot.PlotControl = this;
            ResetMultiplot(newPlot);
            _internalPlot = newPlot;
        }
    }

    private void ResetMultiplot(SP.Plot plot)
    {
        while (_multiplotImpl.Subplots.Count > 0)
        {
            _multiplotImpl.Subplots.RemoveAt(0);
        }
        _multiplotImpl.Subplots.Add(plot);
    }

    private class CustomDrawOp : ICustomDrawOperation
    {
        private readonly SP.Multiplot _multiplot;
        private readonly float _displayScale;

        public Rect Bounds { get; }
        public bool HitTest(Point p) => true;
        public bool Equals(ICustomDrawOperation? other) => false;

        public CustomDrawOp(Rect bounds, SP.Multiplot multiplot, float displayScale)
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
            SP.PixelRect rect = new(0, (float)Bounds.Width * _displayScale,
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
        CustomDrawOp customDrawOp = new(controlBounds, _multiplotImpl, DisplayScale);
        context.Custom(customDrawOp);
    }

    public void Reset()
    {
        SP.Plot plot = new() { PlotControl = this };
        Reset(plot);
    }

    public void Reset(SP.Plot plot)
    {
        SP.Plot oldPlot = _internalPlot;
        _internalPlot = plot;
        Plot = plot;
        oldPlot?.Dispose();
        ResetMultiplot(plot);
    }

    public void Refresh()
    {
        Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
    }

    public void ShowContextMenu(SP.Pixel position)
    {
        Menu?.ShowContextMenu(position);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        SP.Pixel pixel = e.ToPixel(this);
        PointerUpdateKind kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
        UserInputProcessor.ProcessMouseDown(pixel, kind);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        SP.Pixel pixel = e.ToPixel(this);
        PointerUpdateKind kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
        UserInputProcessor.ProcessMouseUp(pixel, kind);
        e.Pointer.Capture(null);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        SP.Pixel pixel = e.ToPixel(this);
        UserInputProcessor.ProcessMouseMove(pixel);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        SP.Pixel pixel = e.ToPixel(this);
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

    public void SetCursor(SP.Cursor cursor)
    {
        Cursor = cursor.GetCursor();
    }
}
