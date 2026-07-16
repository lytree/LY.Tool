using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace LYBox.FluentWindow.Windowing;

/// <summary>
/// Hosts the minimize/maximize/close caption buttons inside a <see cref="FluentWindow"/>
/// control template.
/// </summary>
/// <remarks>
/// Ported from AvaloniaFluentUI. The upstream <c>FACompositeDisposable</c> aggregator has
/// been replaced with two explicit <see cref="IDisposable"/> fields that track the parent
/// window's <see cref="Window.WindowStateProperty"/> and <see cref="Window.IsActiveProperty"/>
/// subscriptions. The control exposes pseudo-classes <c>:maximized</c>, <c>:fullscreen</c>
/// and <c>:inactive</c> that template authors can style against.
/// </remarks>
public class MinMaxCloseControl : TemplatedControl
{
    private Button? _minimizeButton;
    private Button? _maxRestoreButton;
    private Button? _closeButton;

    // Replaces the upstream FACompositeDisposable. One subscription per observed property.
    private IDisposable? _windowStateSubscription;
    private IDisposable? _windowActiveSubscription;

    /// <summary>Visibility of the Minimize caption button.</summary>
    public static readonly StyledProperty<bool> MinButtonIsVisibleProperty =
        AvaloniaProperty.Register<MinMaxCloseControl, bool>(nameof(MinButtonIsVisible), defaultValue: true);

    /// <summary>Visibility of the Maximize/Restore caption button.</summary>
    public static readonly StyledProperty<bool> MaxButtonIsVisibleProperty =
        AvaloniaProperty.Register<MinMaxCloseControl, bool>(nameof(MaxButtonIsVisible), defaultValue: true);

    /// <summary>Visibility of the Close caption button.</summary>
    public static readonly StyledProperty<bool> CloseButtonIsVisibleProperty =
        AvaloniaProperty.Register<MinMaxCloseControl, bool>(nameof(CloseButtonIsVisible), defaultValue: true);

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

    public MinMaxCloseControl()
    {
    }

    private FluentWindow? ParentWindow => TemplatedParent as FluentWindow;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        DetachButtons();
        base.OnApplyTemplate(e);

        _minimizeButton = e.NameScope.Find<Button>("MinimizeButton");
        _maxRestoreButton = e.NameScope.Find<Button>("MaxRestoreButton");
        _closeButton = e.NameScope.Find<Button>("CloseButton");

        if (_minimizeButton is not null)
            _minimizeButton.Click += OnMinimizeClick;
        if (_maxRestoreButton is not null)
            _maxRestoreButton.Click += OnMaximizeClick;
        if (_closeButton is not null)
            _closeButton.Click += OnCloseClick;

        SubscribeParent();
        UpdateWindowState();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeParent();
        UpdateWindowState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UnsubscribeParent();
    }

    private void SubscribeParent()
    {
        var parent = ParentWindow;
        if (parent is null) return;

        UnsubscribeParent();

        parent.PropertyChanged += OnParentPropertyChanged;
        _windowStateSubscription = new EventDisposable(
            () => parent.PropertyChanged -= OnParentPropertyChanged);
        _windowActiveSubscription = null;
    }

    private void OnParentPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty || e.Property == Window.IsActiveProperty)
            UpdateWindowState();
    }

    private void UnsubscribeParent()
    {
        _windowStateSubscription?.Dispose();
        _windowStateSubscription = null;
        _windowActiveSubscription?.Dispose();
        _windowActiveSubscription = null;
    }

    private void DetachButtons()
    {
        if (_minimizeButton is not null)
        {
            _minimizeButton.Click -= OnMinimizeClick;
            _minimizeButton = null;
        }
        if (_maxRestoreButton is not null)
        {
            _maxRestoreButton.Click -= OnMaximizeClick;
            _maxRestoreButton = null;
        }
        if (_closeButton is not null)
        {
            _closeButton.Click -= OnCloseClick;
            _closeButton = null;
        }
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        if (ParentWindow is { } w)
            w.WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        FakeMaximizeClick();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        ParentWindow?.Close();
    }

    private void UpdateWindowState()
    {
        var parent = ParentWindow;
        if (parent is null) return;

        PseudoClasses.Set(":maximized", parent.WindowState == WindowState.Maximized);
        PseudoClasses.Set(":fullscreen", parent.WindowState == WindowState.FullScreen);
        PseudoClasses.Set(":inactive", !parent.IsActive);
    }

    /// <summary>
    /// Returns true if the supplied point (in this control's coordinate space) lies
    /// within one of the caption buttons. Used by the host window to decide whether a
    /// pointer press in the title bar should reach the buttons or initiate dragging.
    /// </summary>
    public bool HitTest(Point point)
    {
        if (IsPointInButton(_minimizeButton, point)) return true;
        if (IsPointInButton(_maxRestoreButton, point)) return true;
        if (IsPointInButton(_closeButton, point)) return true;
        return false;
    }

    private bool IsPointInButton(Button? button, Point point)
    {
        if (button is null) return false;
        // Translate the point from this control's coordinate space into the button's,
        // then check it falls within the button's [0, width] × [0, height] local box.
        var p = this.TranslatePoint(point, button);
        return p is not null
            && p.Value.X >= 0
            && p.Value.X <= button.Bounds.Width
            && p.Value.Y >= 0
            && p.Value.Y <= button.Bounds.Height;
    }

    /// <summary>
    /// Simulates the Maximize/Restore button being hovered. Used by host chrome that
    /// routes hover state through hit-testing rather than direct pointer events.
    /// </summary>
    public void FakeMaximizeHover(bool hovered)
    {
        PseudoClasses.Set(":fakeMaximizeHover", hovered);
    }

    /// <summary>
    /// Simulates the Maximize/Restore button being pressed. Used by host chrome that
    /// routes pressed state through hit-testing rather than direct pointer events.
    /// </summary>
    public void FakeMaximizePressed(bool pressed)
    {
        PseudoClasses.Set(":fakeMaximizePressed", pressed);
    }

    /// <summary>
    /// Toggles maximize/restore as if the user clicked the Maximize button.
    /// </summary>
    public void FakeMaximizeClick()
    {
        if (ParentWindow is { } w)
        {
            w.WindowState = w.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }

    /// <summary>
    /// Wraps an event-handler unsubscription action as an <see cref="IDisposable"/>,
    /// replacing the upstream <c>FACompositeDisposable</c> aggregator with two explicit
    /// disposable fields.
    /// </summary>
    private sealed class EventDisposable : IDisposable
    {
        private Action? _unsubscribe;
        public EventDisposable(Action unsubscribe) => _unsubscribe = unsubscribe;
        public void Dispose()
        {
            _unsubscribe?.Invoke();
            _unsubscribe = null;
        }
    }
}
