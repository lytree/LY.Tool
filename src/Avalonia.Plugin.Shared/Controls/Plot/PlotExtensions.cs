using Avalonia;
using Avalonia.Input;
using ScottPlot;
using ScottPlot.Interactivity;
using ScottPlot.Interactivity.UserActions;

namespace Avalonia.Plugin.Shared.Controls;

internal static class PlotExtensions
{
    internal static ScottPlot.Pixel ToPixel(this PointerEventArgs e, Visual visual)
    {
        float x = (float)e.GetPosition(visual).X;
        float y = (float)e.GetPosition(visual).Y;
        return new ScottPlot.Pixel(x, y);
    }

    internal static void ProcessMouseDown(this UserInputProcessor processor, ScottPlot.Pixel pixel, PointerUpdateKind kind)
    {
        IUserAction action = kind switch
        {
            PointerUpdateKind.LeftButtonPressed => new LeftMouseDown(pixel),
            PointerUpdateKind.MiddleButtonPressed => new MiddleMouseDown(pixel),
            PointerUpdateKind.RightButtonPressed => new RightMouseDown(pixel),
            _ => new Unknown("mouse down", kind.ToString()),
        };
        processor.Process(action);
    }

    internal static void ProcessMouseUp(this UserInputProcessor processor, ScottPlot.Pixel pixel, PointerUpdateKind kind)
    {
        IUserAction action = kind switch
        {
            PointerUpdateKind.LeftButtonReleased => new LeftMouseUp(pixel),
            PointerUpdateKind.MiddleButtonReleased => new MiddleMouseUp(pixel),
            PointerUpdateKind.RightButtonReleased => new RightMouseUp(pixel),
            _ => new Unknown("mouse up", kind.ToString()),
        };
        processor.Process(action);
    }

    internal static void ProcessMouseMove(this UserInputProcessor processor, ScottPlot.Pixel pixel)
    {
        processor.Process(new MouseMove(pixel));
    }

    internal static void ProcessMouseWheel(this UserInputProcessor processor, ScottPlot.Pixel pixel, double delta)
    {
        IUserAction action = delta > 0
            ? new MouseWheelUp(pixel)
            : new MouseWheelDown(pixel);
        processor.Process(action);
    }

    internal static void ProcessKeyDown(this UserInputProcessor processor, KeyEventArgs e)
    {
        ScottPlot.Interactivity.Key key = GetKey(e.Key);
        IUserAction action = new KeyDown(key);
        processor.Process(action);
    }

    internal static void ProcessKeyUp(this UserInputProcessor processor, KeyEventArgs e)
    {
        ScottPlot.Interactivity.Key key = GetKey(e.Key);
        IUserAction action = new KeyUp(key);
        processor.Process(action);
    }

    public static ScottPlot.Interactivity.Key GetKey(Input.Key avaKey)
    {
        return avaKey switch
        {
            Input.Key.LeftAlt => StandardKeys.Alt,
            Input.Key.RightAlt => StandardKeys.Alt,
            Input.Key.LeftShift => StandardKeys.Shift,
            Input.Key.RightShift => StandardKeys.Shift,
            Input.Key.LeftCtrl => StandardKeys.Control,
            Input.Key.RightCtrl => StandardKeys.Control,
            _ => new ScottPlot.Interactivity.Key(avaKey.ToString()),
        };
    }

    public static Input.Cursor GetCursor(this ScottPlot.Cursor cursor)
    {
        return cursor switch
        {
            ScottPlot.Cursor.Arrow => new(StandardCursorType.Arrow),
            ScottPlot.Cursor.No => new(StandardCursorType.No),
            ScottPlot.Cursor.Wait => new(StandardCursorType.Wait),
            ScottPlot.Cursor.Hand => new(StandardCursorType.Hand),
            ScottPlot.Cursor.Cross => new(StandardCursorType.Cross),
            ScottPlot.Cursor.SizeAll => new(StandardCursorType.SizeAll),
            ScottPlot.Cursor.SizeNorthSouth => new(StandardCursorType.SizeNorthSouth),
            ScottPlot.Cursor.SizeWestEast => new(StandardCursorType.SizeWestEast),
            _ => new(StandardCursorType.Arrow),
        };
    }
}
