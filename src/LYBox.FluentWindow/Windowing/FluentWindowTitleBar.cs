using Avalonia.Media;

namespace LYBox.FluentWindow.Windowing;

/// <summary>
/// Holds color and layout customizations for the title bar of a <see cref="FluentWindow"/>.
/// </summary>
/// <remarks>
/// This is a plain CLR object (not a control). It is constructed internally by
/// <see cref="FluentWindow"/> and notifies the parent window of color/height changes by
/// invoking <see cref="FluentWindow.TitleBarColorsChanged"/> so the host can refresh
/// native chrome or pseudo-classes as needed.
/// Ported from AvaloniaFluentUI — <see cref="System.Math"/> is used for floating-point
/// comparison instead of the upstream <c>MathHelpers</c> utility.
/// </remarks>
public class FluentWindowTitleBar
{
    private readonly FluentWindow _parent;

    private Color? _backgroundColor;
    private Color? _foregroundColor;
    private Color? _inactiveBackgroundColor;
    private Color? _inactiveForegroundColor;
    private Color? _buttonBackgroundColor;
    private Color? _buttonForegroundColor;
    private Color? _buttonHoverBackgroundColor;
    private Color? _buttonHoverForegroundColor;
    private Color? _buttonPressedBackgroundColor;
    private Color? _buttonPressedForegroundColor;
    private Color? _buttonInactiveBackgroundColor;
    private Color? _buttonInactiveForegroundColor;
    private double _height = 45;

    internal FluentWindowTitleBar(FluentWindow parent)
    {
        _parent = parent;
    }

    /// <summary>Background color of the title bar when the window is active.</summary>
    public Color? BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            if (!Nullable.Equals(_backgroundColor, value))
            {
                _backgroundColor = value;
                _parent.TitleBarColorsChanged();
            }
        }
    }

    /// <summary>Foreground (text/icon) color of the title bar when the window is active.</summary>
    public Color? ForegroundColor
    {
        get => _foregroundColor;
        set
        {
            if (!Nullable.Equals(_foregroundColor, value))
            {
                _foregroundColor = value;
                _parent.TitleBarColorsChanged();
            }
        }
    }

    /// <summary>Background color of the title bar when the window is inactive.</summary>
    public Color? InactiveBackgroundColor
    {
        get => _inactiveBackgroundColor;
        set
        {
            if (!Nullable.Equals(_inactiveBackgroundColor, value))
            {
                _inactiveBackgroundColor = value;
                _parent.TitleBarColorsChanged();
            }
        }
    }

    /// <summary>Foreground (text/icon) color of the title bar when the window is inactive.</summary>
    public Color? InactiveForegroundColor
    {
        get => _inactiveForegroundColor;
        set
        {
            if (!Nullable.Equals(_inactiveForegroundColor, value))
            {
                _inactiveForegroundColor = value;
                _parent.TitleBarColorsChanged();
            }
        }
    }

    /// <summary>Default background color of caption buttons (min/max/close).</summary>
    public Color? ButtonBackgroundColor
    {
        get => _buttonBackgroundColor;
        set
        {
            if (!Nullable.Equals(_buttonBackgroundColor, value))
            {
                _buttonBackgroundColor = value;
                _parent.TitleBarColorsChanged();
            }
        }
    }

    /// <summary>Default foreground (glyph) color of caption buttons.</summary>
    public Color? ButtonForegroundColor
    {
        get => _buttonForegroundColor;
        set
        {
            if (!Nullable.Equals(_buttonForegroundColor, value))
            {
                _buttonForegroundColor = value;
                _parent.TitleBarColorsChanged();
            }
        }
    }

    /// <summary>Background color of caption buttons when the pointer hovers them.</summary>
    public Color? ButtonHoverBackgroundColor
    {
        get => _buttonHoverBackgroundColor;
        set
        {
            if (!Nullable.Equals(_buttonHoverBackgroundColor, value))
            {
                _buttonHoverBackgroundColor = value;
                _parent.TitleBarColorsChanged();
            }
        }
    }

    /// <summary>Foreground color of caption buttons when the pointer hovers them.</summary>
    public Color? ButtonHoverForegroundColor
    {
        get => _buttonHoverForegroundColor;
        set
        {
            if (!Nullable.Equals(_buttonHoverForegroundColor, value))
            {
                _buttonHoverForegroundColor = value;
                _parent.TitleBarColorsChanged();
            }
        }
    }

    /// <summary>Background color of caption buttons when pressed.</summary>
    public Color? ButtonPressedBackgroundColor
    {
        get => _buttonPressedBackgroundColor;
        set
        {
            if (!Nullable.Equals(_buttonPressedBackgroundColor, value))
            {
                _buttonPressedBackgroundColor = value;
                _parent.TitleBarColorsChanged();
            }
        }
    }

    /// <summary>Foreground color of caption buttons when pressed.</summary>
    public Color? ButtonPressedForegroundColor
    {
        get => _buttonPressedForegroundColor;
        set
        {
            if (!Nullable.Equals(_buttonPressedForegroundColor, value))
            {
                _buttonPressedForegroundColor = value;
                _parent.TitleBarColorsChanged();
            }
        }
    }

    /// <summary>Background color of caption buttons when the window is inactive.</summary>
    public Color? ButtonInactiveBackgroundColor
    {
        get => _buttonInactiveBackgroundColor;
        set
        {
            if (!Nullable.Equals(_buttonInactiveBackgroundColor, value))
            {
                _buttonInactiveBackgroundColor = value;
                _parent.TitleBarColorsChanged();
            }
        }
    }

    /// <summary>Foreground color of caption buttons when the window is inactive.</summary>
    public Color? ButtonInactiveForegroundColor
    {
        get => _buttonInactiveForegroundColor;
        set
        {
            if (!Nullable.Equals(_buttonInactiveForegroundColor, value))
            {
                _buttonInactiveForegroundColor = value;
                _parent.TitleBarColorsChanged();
            }
        }
    }

    /// <summary>Height of the title bar in device-independent pixels. Default is 45.</summary>
    public double Height
    {
        get => _height;
        set
        {
            // Using System.Math for floating-point comparison (no MathHelpers dependency).
            if (System.Math.Abs(_height - value) > double.Epsilon)
            {
                _height = value;
                _parent.TitleBarColorsChanged();
            }
        }
    }
}
