using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace LYBox.FluentWindow.Windowing;

/// <summary>
/// Linux platform initialization and chrome handling for <see cref="FluentWindow"/>.
/// </summary>
/// <remarks>
/// Ported from the AvaloniaFluentUI repository. Because Avalonia's X11 backend does not
/// remove the WM title bar when <c>WindowDecorations.BorderOnly</c> is used, the host is
/// expected to extend the client area to cover it (see
/// <c>DefaultWindowChromeService.ApplyChrome</c>). This partial supplies:
/// <list type="bullet">
///   <item>A system menu (<see cref="MenuFlyout"/>) bound to the window's context flyout.</item>
///   <item>Manual window-edge hit-testing for resizing (since the WM resize frame may be
///   covered by the extended client area).</item>
/// </list>
/// </remarks>
public partial class FluentWindow
{
    private MenuFlyout? _systemMenuFlyout;
    private ResizeDirection _currentResizeDirection = ResizeDirection.None;

    /// <summary>Thickness (in DIPs) of the window border used for resize hit-testing.</summary>
    private const double ResizeBorderSize = 6;

    partial void InitializeLinuxPlatform()
    {
        IsLinux = true;
        Loaded += OnLinuxLoaded;
    }

    private void OnLinuxLoaded(object? sender, RoutedEventArgs e)
    {
        BuildSystemMenu();
        SetupResizeBorders();
    }

    /// <summary>
    /// Builds the system menu (restore / move / size / minimize / maximize / close) and
    /// attaches it as the window's <see cref="ContextMenu"/>.
    /// </summary>
    private void BuildSystemMenu()
    {
        var menu = new MenuFlyout();

        var restoreItem = new MenuItem { Header = "Restore" };
        restoreItem.Click += (_, _) => WindowState = WindowState.Normal;
        menu.Items.Add(restoreItem);

        var moveItem = new MenuItem { Header = "Move" };
        // Move must be initiated by a pointer event on the title bar — nothing to do here.
        menu.Items.Add(moveItem);

        var sizeItem = new MenuItem { Header = "Size" };
        // Size must be initiated by a pointer event on a resize border — nothing to do here.
        menu.Items.Add(sizeItem);

        var minimizeItem = new MenuItem { Header = "Minimize" };
        minimizeItem.Click += (_, _) => WindowState = WindowState.Minimized;
        menu.Items.Add(minimizeItem);

        var maximizeItem = new MenuItem { Header = "Maximize" };
        maximizeItem.Click += (_, _) => WindowState = WindowState.Maximized;
        menu.Items.Add(maximizeItem);

        var closeItem = new MenuItem { Header = "Close" };
        closeItem.Click += (_, _) => Close();
        menu.Items.Add(closeItem);

        _systemMenuFlyout = menu;
        ContextFlyout = menu;
    }

    /// <summary>
    /// Hooks pointer events used to detect window-edge resizing.
    /// </summary>
    private void SetupResizeBorders()
    {
        PointerPressed += OnResizePointerPressed;
        PointerMoved += OnResizePointerMoved;
    }

    private void OnResizePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (WindowState is WindowState.Maximized or WindowState.FullScreen)
            return;

        var props = e.GetCurrentPoint(this).Properties;
        if (props.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            return;

        var point = e.GetPosition(this);
        _currentResizeDirection = GetResizeDirection(point);
        if (_currentResizeDirection == ResizeDirection.None)
            return;

        var edge = ToWindowEdge(_currentResizeDirection);
        try
        {
            BeginResizeDrag(edge, e);
            e.Handled = true;
        }
        catch
        {
            // BeginResizeDrag throws when the window manager is unavailable or the
            // pointer event cannot be used to initiate a server-side resize.
        }
    }

    private void OnResizePointerMoved(object? sender, PointerEventArgs e)
    {
        if (WindowState is WindowState.Maximized or WindowState.FullScreen)
            return;

        // Skip updating the cursor when a button is pressed — the user is likely dragging.
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsLeftButtonPressed)
            return;

        var point = e.GetPosition(this);
        var dir = GetResizeDirection(point);
        UpdateCursor(dir);
    }

    /// <summary>
    /// Maps a point (in window coordinate space) to a <see cref="ResizeDirection"/> based
    /// on its proximity to the window edges.
    /// </summary>
    private ResizeDirection GetResizeDirection(Point point)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;

        bool isLeft = point.X <= ResizeBorderSize && point.X >= 0;
        bool isRight = point.X >= width - ResizeBorderSize && point.X <= width;
        bool isTop = point.Y <= ResizeBorderSize && point.Y >= 0;
        bool isBottom = point.Y >= height - ResizeBorderSize && point.Y <= height;

        if (isTop && isLeft) return ResizeDirection.TopLeft;
        if (isTop && isRight) return ResizeDirection.TopRight;
        if (isBottom && isLeft) return ResizeDirection.BottomLeft;
        if (isBottom && isRight) return ResizeDirection.BottomRight;
        if (isTop) return ResizeDirection.Top;
        if (isBottom) return ResizeDirection.Bottom;
        if (isLeft) return ResizeDirection.Left;
        if (isRight) return ResizeDirection.Right;

        return ResizeDirection.None;
    }

    /// <summary>
    /// Converts a <see cref="ResizeDirection"/> into the corresponding Avalonia
    /// <see cref="WindowEdge"/> used by <see cref="Window.BeginResizeDrag"/>.
    /// </summary>
    private static WindowEdge ToWindowEdge(ResizeDirection direction)
    {
        return direction switch
        {
            ResizeDirection.Top => WindowEdge.North,
            ResizeDirection.Bottom => WindowEdge.South,
            ResizeDirection.Left => WindowEdge.West,
            ResizeDirection.Right => WindowEdge.East,
            ResizeDirection.TopLeft => WindowEdge.NorthWest,
            ResizeDirection.TopRight => WindowEdge.NorthEast,
            ResizeDirection.BottomLeft => WindowEdge.SouthWest,
            ResizeDirection.BottomRight => WindowEdge.SouthEast,
            _ => WindowEdge.East
        };
    }

    /// <summary>
    /// Updates <see cref="InputElement.Cursor"/> based on the current resize direction so
    /// the user gets the standard size-handle cursor over each window edge.
    /// </summary>
    private void UpdateCursor(ResizeDirection direction)
    {
        Cursor = direction switch
        {
            ResizeDirection.Top or ResizeDirection.Bottom
                => new Cursor(StandardCursorType.SizeNorthSouth),
            ResizeDirection.Left or ResizeDirection.Right
                => new Cursor(StandardCursorType.SizeWestEast),
            ResizeDirection.TopLeft
                => new Cursor(StandardCursorType.TopLeftCorner),
            ResizeDirection.TopRight
                => new Cursor(StandardCursorType.TopRightCorner),
            ResizeDirection.BottomLeft
                => new Cursor(StandardCursorType.BottomLeftCorner),
            ResizeDirection.BottomRight
                => new Cursor(StandardCursorType.BottomRightCorner),
            _ => null
        };
    }

    /// <summary>
    /// Window edge used by the resize hit-test. <see cref="None"/> means "not on a resize border".
    /// </summary>
    private enum ResizeDirection
    {
        None,
        Top,
        Bottom,
        Left,
        Right,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
}
