using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace LYBox.FluentWindow.Windowing;

/// <summary>
/// A <see cref="Window"/> that renders a custom-drawn Fluent-style title bar and chrome.
/// </summary>
/// <remarks>
/// Simplified port of the AvaloniaFluentUI <c>FluentWindow</c>. The upstream dependencies
/// on <c>AvaloniaFluentUI.Controls</c>, <c>AvaloniaFluentUI.Core</c>,
/// <c>AvaloniaFluentUI.Styling</c> and <c>AvaloniaFluentUI.Controls.Interop</c> have been
/// removed — platform-specific behavior is provided by the partial files
/// <c>FluentWindow.win32.cs</c> and <c>FluentWindow.linux.cs</c>, and Win32 features are
/// stubbed in <see cref="Win32.Win32FluentWindowFeatures"/>.
/// </remarks>
[TemplatePart("PART_DefaultTitleBar", typeof(Grid))]
[TemplatePart("PART_MinimizeButton", typeof(Button))]
[TemplatePart("PART_MaximizeButton", typeof(Button))]
[TemplatePart("PART_CloseButton", typeof(Button))]
[TemplatePart("RootBorder", typeof(Border))]
public partial class FluentWindow : Window
{
    private readonly FluentWindowTitleBar _titleBar;

    private Grid? _defaultTitleBar;
    private Button? _minimizeButton;
    private Button? _maximizeButton;
    private Button? _closeButton;
    private Border? _rootBorder;

    /// <summary>
    /// Window icon. Declared with <c>new</c> so a FluentWindow control theme can bind
    /// to it independently of the base <see cref="Window.IconProperty"/> registration.
    /// </summary>
    public new static readonly StyledProperty<IImage?> IconProperty =
        AvaloniaProperty.Register<FluentWindow, IImage?>(nameof(Icon));

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<FluentWindow, double>(nameof(IconSize), defaultValue: 16);

    public static readonly StyledProperty<double> TitleBarHeightProperty =
        AvaloniaProperty.Register<FluentWindow, double>(nameof(TitleBarHeight));

    public static readonly StyledProperty<bool> TitleBarIsVisibleProperty =
        AvaloniaProperty.Register<FluentWindow, bool>(nameof(TitleBarIsVisible), defaultValue: true);

    public static readonly StyledProperty<bool> TitleBarContentIsVisibleProperty =
        AvaloniaProperty.Register<FluentWindow, bool>(nameof(TitleBarContentIsVisible), defaultValue: true);

    public static readonly StyledProperty<object?> TitleBarContentProperty =
        AvaloniaProperty.Register<FluentWindow, object?>(nameof(TitleBarContent));

    public static readonly StyledProperty<Thickness> TitleBarContentMarginProperty =
        AvaloniaProperty.Register<FluentWindow, Thickness>(
            nameof(TitleBarContentMargin),
            defaultValue: new Thickness(8, 0, 140, 0));

    public static readonly StyledProperty<Thickness> TitleBarMarginProperty =
        AvaloniaProperty.Register<FluentWindow, Thickness>(nameof(TitleBarMargin));

    public static readonly StyledProperty<bool> MinButtonIsVisibleProperty =
        AvaloniaProperty.Register<FluentWindow, bool>(nameof(MinButtonIsVisible), defaultValue: true);

    public static readonly StyledProperty<bool> MaxButtonIsVisibleProperty =
        AvaloniaProperty.Register<FluentWindow, bool>(nameof(MaxButtonIsVisible), defaultValue: true);

    public static readonly StyledProperty<bool> CloseButtonIsVisibleProperty =
        AvaloniaProperty.Register<FluentWindow, bool>(nameof(CloseButtonIsVisible), defaultValue: true);

    public static readonly StyledProperty<bool> FullScreenButtonIsVisibleProperty =
        AvaloniaProperty.Register<FluentWindow, bool>(nameof(FullScreenButtonIsVisible));

    public static readonly StyledProperty<bool> ShowAsDialogProperty =
        AvaloniaProperty.Register<FluentWindow, bool>(nameof(ShowAsDialog));

    /// <summary>
    /// Attached property: opt a control hosted inside the title bar into receiving pointer
    /// input (i.e. exclude it from the title-bar drag hit-test).
    /// </summary>
    public static readonly AttachedProperty<bool> AllowInteractionInTitleBarProperty =
        AvaloniaProperty.RegisterAttached<FluentWindow, Control, bool>(
            "AllowInteractionInTitleBar",
            defaultValue: false);

    static FluentWindow()
    {
        // Default value override to ensure the shadowed Icon property reports null by default.
        IconProperty.OverrideDefaultValue<FluentWindow>(null);
    }

    public FluentWindow()
    {
        _titleBar = new FluentWindowTitleBar(this);

        // Pseudo-classes used by the default FluentWindow control theme.
        PseudoClasses.Add(":noFullScreen");

        if (!Design.IsDesignMode)
        {
            if (OperatingSystem.IsWindows())
                InitializeWindowPlatform();
            else if (OperatingSystem.IsLinux())
                InitializeLinuxPlatform();
        }
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(FluentWindow);

    /// <summary>Color and height customizations for the title bar.</summary>
    public FluentWindowTitleBar TitleBar => _titleBar;

    /// <summary>Platform-specific window chrome features, or null on unsupported platforms.</summary>
    public IAppWindowPlatformFeatures? PlatformFeatures { get; protected set; }

    /// <summary>
    /// Optional splash screen. The host is responsible for showing/hiding it via
    /// <see cref="SplashScreen.SplashScreenContext"/>.
    /// </summary>
    public IApplicationSplashScreen? SplashScreen { get; set; }

    /// <summary>True when the current platform is Windows.</summary>
    protected internal bool IsWindows { get; set; }

    /// <summary>True when the current platform is Windows 11 (build 22000+).</summary>
    protected internal bool IsWindows11 { get; set; }

    /// <summary>True when the current platform is Linux.</summary>
    protected internal bool IsLinux { get; set; }

    /// <summary>Window icon. Shadowed so control themes can bind to FluentWindow.Icon.</summary>
    public new IImage? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public double TitleBarHeight
    {
        get => GetValue(TitleBarHeightProperty);
        set => SetValue(TitleBarHeightProperty, value);
    }

    public bool TitleBarIsVisible
    {
        get => GetValue(TitleBarIsVisibleProperty);
        set => SetValue(TitleBarIsVisibleProperty, value);
    }

    public bool TitleBarContentIsVisible
    {
        get => GetValue(TitleBarContentIsVisibleProperty);
        set => SetValue(TitleBarContentIsVisibleProperty, value);
    }

    public object? TitleBarContent
    {
        get => GetValue(TitleBarContentProperty);
        set => SetValue(TitleBarContentProperty, value);
    }

    public Thickness TitleBarContentMargin
    {
        get => GetValue(TitleBarContentMarginProperty);
        set => SetValue(TitleBarContentMarginProperty, value);
    }

    public Thickness TitleBarMargin
    {
        get => GetValue(TitleBarMarginProperty);
        set => SetValue(TitleBarMarginProperty, value);
    }

    public bool MinButtonIsVisible
    {
        get => GetValue(MinButtonIsVisibleProperty);
        set => SetValue(MinButtonIsVisibleProperty, value);
    }

    public bool MaxButtonIsVisible
    {
        get => GetValue(MaxButtonIsVisibleProperty);
        set => SetValue(MaxButtonIsVisibleProperty, value);
    }

    public bool CloseButtonIsVisible
    {
        get => GetValue(CloseButtonIsVisibleProperty);
        set => SetValue(CloseButtonIsVisibleProperty, value);
    }

    public bool FullScreenButtonIsVisible
    {
        get => GetValue(FullScreenButtonIsVisibleProperty);
        set => SetValue(FullScreenButtonIsVisibleProperty, value);
    }

    /// <summary>
    /// When true, applies the <c>:dialog</c> pseudo-class so the control theme can render
    /// the window as a modal dialog (e.g. removing min/max buttons).
    /// </summary>
    public bool ShowAsDialog
    {
        get => GetValue(ShowAsDialogProperty);
        set
        {
            SetValue(ShowAsDialogProperty, value);
            PseudoClasses.Set(":dialog", value);
        }
    }

    /// <summary>Gets the attached <c>AllowInteractionInTitleBar</c> value for a control.</summary>
    public static bool GetAllowInteractionInTitleBar(Control element) =>
        element.GetValue(AllowInteractionInTitleBarProperty);

    /// <summary>Sets the attached <c>AllowInteractionInTitleBar</c> value for a control.</summary>
    public static void SetAllowInteractionInTitleBar(Control element, bool value) =>
        element.SetValue(AllowInteractionInTitleBarProperty, value);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _defaultTitleBar = e.NameScope.Find<Grid>("PART_DefaultTitleBar");
        _minimizeButton = e.NameScope.Find<Button>("PART_MinimizeButton");
        _maximizeButton = e.NameScope.Find<Button>("PART_MaximizeButton");
        _closeButton = e.NameScope.Find<Button>("PART_CloseButton");
        _rootBorder = e.NameScope.Find<Border>("RootBorder");
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled)
        {
            OnWindowPointerPressed(e);
        }
    }

    /// <summary>
    /// Handles title bar hit-testing, double-click to maximize, and drag-move.
    /// Invoked from <see cref="OnPointerPressed"/> when the base handler did not handle the event.
    /// </summary>
    protected virtual void OnWindowPointerPressed(PointerPressedEventArgs e)
    {
        if (!TitleBarIsVisible) return;

        var props = e.GetCurrentPoint(this).Properties;
        if (props.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed) return;

        var point = e.GetPosition(this);
        if (!HitTestTitleBar(point)) return;

        // If the pointer-down is over a control that opted into title-bar interaction,
        // let the control receive the event rather than initiating a drag.
        if (e.Source is Visual source && HasTitleBarInteraction(source))
        {
            return;
        }

        // Double-click inside the draggable title bar toggles maximize/restore.
        if (e.ClickCount >= 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            e.Handled = true;
            return;
        }

        try
        {
            BeginMoveDrag(e);
            e.Handled = true;
        }
        catch
        {
            // BeginMoveDrag throws when the window manager is not ready or the platform
            // does not support server-side dragging. Swallow to keep the UI responsive.
        }
    }

    /// <summary>
    /// Returns true if the supplied point (in window coordinate space) lies within the
    /// draggable title bar region and a drag should be initiated.
    /// </summary>
    public bool HitTestTitleBar(Point point)
    {
        if (_defaultTitleBar is null) return false;
        return _defaultTitleBar.Bounds.Contains(point);
    }

    private static bool HasTitleBarInteraction(Visual visual)
    {
        var current = visual;
        while (current is not null)
        {
            if (current is Control c && GetAllowInteractionInTitleBar(c))
                return true;
            current = current.GetVisualParent<Visual>();
        }
        return false;
    }

    /// <summary>
    /// Toggles the acrylic (blurred translucent) backdrop on Windows. Stubbed in this port.
    /// </summary>
    public void EnabledAcrylicBlue(bool enable)
    {
        // No-op: real implementation would configure the Win32 acrylic backdrop via
        // DwmSetWindowAttribute or the Win32 compositor interface.
    }

    /// <summary>
    /// Toggles the Mica backdrop on Windows 11. Stubbed in this port.
    /// </summary>
    public void EnabledMica(bool enable)
    {
        // No-op: real implementation would call DwmSetWindowAttribute with
        // DWMWA_SYSTEMBACKDROP_TYPE = DWMSBT_MAINWINDOW on Windows 11.
    }

    /// <summary>
    /// Invoked by <see cref="FluentWindowTitleBar"/> whenever a color or the height changes.
    /// The simplified port performs no native chrome sync; override to push colors to the
    /// platform chrome (e.g. via <see cref="PlatformFeatures"/>).
    /// </summary>
    public virtual void TitleBarColorsChanged()
    {
        // No-op in the simplified port.
    }

    /// <summary>
    /// Invoked when <see cref="TitleBarHeight"/> changes. The simplified port performs no
    /// layout adjustment; override to update window metrics if needed.
    /// </summary>
    protected virtual void OnTitleBarHeightChanged(double newHeight)
    {
        // No-op in the simplified port.
    }

    // Partial methods implemented in FluentWindow.win32.cs / FluentWindow.linux.cs.
    partial void InitializeWindowPlatform();
    partial void InitializeLinuxPlatform();
}
