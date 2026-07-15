using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using LYBox.Platforms.Abstraction;

namespace LYBox.UI.Views;

/// <summary>
/// 自绘 Fluent 标题栏。
/// </summary>
/// <remarks>
/// 仅在当前平台不支持扩展客户区（Linux）时可见（由 <see cref="IWindowChromeService.NeedsSelfDrawnTitleBar"/> 决定）。
/// 在该模式下，窗口使用 <c>WindowDecorations.BorderOnly</c>（无原生标题栏、保留 WM 缩放边框与阴影），
/// 本控件承担标题栏职责：拖动移动（<see cref="Window.BeginMoveDrag"/>）、标题展示、最小化/最大化/关闭按钮。
/// 双击拖动区域可在最大化与正常状态间切换。
/// </remarks>
public partial class FluentTitleBar : UserControl
{
    private Window? _window;

    public FluentTitleBar()
    {
        InitializeComponent();

        // 仅在需要自绘标题栏的平台上显示；其余平台由 Avalonia 的 WindowDrawnDecorations 绘制标题栏。
        IsVisible = PlatformServices.WindowChromeService.NeedsSelfDrawnTitleBar;

        var dragRegion = this.Get<Border>("PART_DragRegion");
        dragRegion.PointerPressed += OnDragRegionPointerPressed;

        this.Get<Button>("PART_MinimizeButton").Click += OnMinimizeClick;
        this.Get<Button>("PART_MaximizeButton").Click += OnMaximizeClick;
        this.Get<Button>("PART_CloseButton").Click += OnCloseClick;

        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _window = this.FindAncestorOfType<Window>();
        if (_window is null) return;

        _window.PropertyChanged += OnWindowPropertyChanged;
        // 初始化最大化/还原图标
        UpdateMaximizeButtonIcon(_window.WindowState);
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_window is null) return;
        if (e.Property == Window.WindowStateProperty)
        {
            UpdateMaximizeButtonIcon(_window.WindowState);
        }
    }

    private void OnDragRegionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_window is null) return;

        var props = e.GetCurrentPoint(this).Properties;
        if (props.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            return;

        // 双击拖动区域：在最大化与正常状态间切换
        if (e.ClickCount >= 2)
        {
            ToggleMaximize();
            return;
        }

        try
        {
            // 走 WM 的移动语义（Linux 上发送 _NET_WM_MOVERESIZE），由窗口管理器处理拖动与边界吸附。
            _window.BeginMoveDrag(e);
        }
        catch
        {
            // BeginMoveDrag 在窗口未就绪或 WM 不支持时会抛出，忽略以保证 UI 不崩。
        }
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        if (_window is null) return;
        _window.WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        _window?.Close();
    }

    private void ToggleMaximize()
    {
        if (_window is null) return;
        _window.WindowState = _window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void UpdateMaximizeButtonIcon(WindowState state)
    {
        var button = this.Get<Button>("PART_MaximizeButton");
        // 最大化时显示还原图标，否则显示最大化图标
        var key = state == WindowState.Maximized ? "FluentIconRestore" : "FluentIconMaximize2";
        button.Content = this.FindResource(key);
    }
}
