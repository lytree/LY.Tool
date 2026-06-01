using Avalonia;
using Avalonia.Input;
using SP = global::ScottPlot;

namespace Avalonia.Plugin.ScottPlot.Controls;

internal static class PlotExtensions
{
    internal static SP.Pixel ToPixel(this PointerEventArgs e, Visual visual)
    {
        float x = (float)e.GetPosition(visual).X;
        float y = (float)e.GetPosition(visual).Y;
        return new SP.Pixel(x, y);
    }

    internal static void ProcessMouseDown(this SP.Interactivity.UserInputProcessor processor, SP.Pixel pixel, PointerUpdateKind kind)
    {
        SP.Interactivity.IUserAction action = kind switch
        {
            PointerUpdateKind.LeftButtonPressed => new SP.Interactivity.UserActions.LeftMouseDown(pixel),
            PointerUpdateKind.MiddleButtonPressed => new SP.Interactivity.UserActions.MiddleMouseDown(pixel),
            PointerUpdateKind.RightButtonPressed => new SP.Interactivity.UserActions.RightMouseDown(pixel),
            _ => new SP.Interactivity.UserActions.Unknown("mouse down", kind.ToString()),
        };
        processor.Process(action);
    }

    internal static void ProcessMouseUp(this SP.Interactivity.UserInputProcessor processor, SP.Pixel pixel, PointerUpdateKind kind)
    {
        SP.Interactivity.IUserAction action = kind switch
        {
            PointerUpdateKind.LeftButtonReleased => new SP.Interactivity.UserActions.LeftMouseUp(pixel),
            PointerUpdateKind.MiddleButtonReleased => new SP.Interactivity.UserActions.MiddleMouseUp(pixel),
            PointerUpdateKind.RightButtonReleased => new SP.Interactivity.UserActions.RightMouseUp(pixel),
            _ => new SP.Interactivity.UserActions.Unknown("mouse up", kind.ToString()),
        };
        processor.Process(action);
    }

    internal static void ProcessMouseMove(this SP.Interactivity.UserInputProcessor processor, SP.Pixel pixel)
    {
        processor.Process(new SP.Interactivity.UserActions.MouseMove(pixel));
    }

    internal static void ProcessMouseWheel(this SP.Interactivity.UserInputProcessor processor, SP.Pixel pixel, double delta)
    {
        SP.Interactivity.IUserAction action = delta > 0
            ? new SP.Interactivity.UserActions.MouseWheelUp(pixel)
            : new SP.Interactivity.UserActions.MouseWheelDown(pixel);
        processor.Process(action);
    }

    internal static void ProcessKeyDown(this SP.Interactivity.UserInputProcessor processor, KeyEventArgs e)
    {
        SP.Interactivity.Key key = GetKey(e.Key);
        SP.Interactivity.IUserAction action = new SP.Interactivity.UserActions.KeyDown(key);
        processor.Process(action);
    }

    internal static void ProcessKeyUp(this SP.Interactivity.UserInputProcessor processor, KeyEventArgs e)
    {
        SP.Interactivity.Key key = GetKey(e.Key);
        SP.Interactivity.IUserAction action = new SP.Interactivity.UserActions.KeyUp(key);
        processor.Process(action);
    }

    public static SP.Interactivity.Key GetKey(Input.Key avaKey)
    {
        return avaKey switch
        {
            Input.Key.LeftAlt => SP.Interactivity.StandardKeys.Alt,
            Input.Key.RightAlt => SP.Interactivity.StandardKeys.Alt,
            Input.Key.LeftShift => SP.Interactivity.StandardKeys.Shift,
            Input.Key.RightShift => SP.Interactivity.StandardKeys.Shift,
            Input.Key.LeftCtrl => SP.Interactivity.StandardKeys.Control,
            Input.Key.RightCtrl => SP.Interactivity.StandardKeys.Control,
            _ => new SP.Interactivity.Key(avaKey.ToString()),
        };
    }

    public static Input.Cursor GetCursor(this SP.Cursor cursor)
    {
        return cursor switch
        {
            SP.Cursor.Arrow => new(StandardCursorType.Arrow),
            SP.Cursor.No => new(StandardCursorType.No),
            SP.Cursor.Wait => new(StandardCursorType.Wait),
            SP.Cursor.Hand => new(StandardCursorType.Hand),
            SP.Cursor.Cross => new(StandardCursorType.Cross),
            SP.Cursor.SizeAll => new(StandardCursorType.SizeAll),
            SP.Cursor.SizeNorthSouth => new(StandardCursorType.SizeNorthSouth),
            SP.Cursor.SizeWestEast => new(StandardCursorType.SizeWestEast),
            _ => new(StandardCursorType.Arrow),
        };
    }
}
