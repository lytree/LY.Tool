using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;
using Ursa.Controls;
using LYBox.UI.ViewModels;

namespace LYBox.UI.Views;

public partial class MainView : UserControl
{
    private MainViewViewModel? _viewModel;
    private Window? _window;

    public MainView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _viewModel = DataContext as MainViewViewModel;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || _viewModel is null)
            return;
        _viewModel.NotificationManager = WindowNotificationManager.TryGetNotificationManager(topLevel, out var manager)
            ? manager
            : new WindowNotificationManager(topLevel);

        // 绑定自绘标题栏的窗口操作
        _window = this.FindAncestorOfType<Window>();
        if (_window is not null)
        {
            _window.PropertyChanged += OnWindowPropertyChanged;
            UpdateMaximizeButtonIcon(_window.WindowState);
        }

        this.Get<Button>("PART_MinimizeButton").Click += OnMinimizeClick;
        this.Get<Button>("PART_MaximizeButton").Click += OnMaximizeClick;
        this.Get<Button>("PART_CloseButton").Click += OnCloseClick;
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_window is null) return;
        if (e.Property == Window.WindowStateProperty)
        {
            UpdateMaximizeButtonIcon(_window.WindowState);
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
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
        var key = state == WindowState.Maximized ? "FluentIconRestore" : "FluentIconMaximize2";
        button.Content = this.FindResource(key);
    }

    private async void OpenRepository(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        var launcher = top.Launcher;
        await launcher.LaunchUriAsync(new Uri("https://github.com/irihitech/Ursa.Avalonia"));
    }
}
