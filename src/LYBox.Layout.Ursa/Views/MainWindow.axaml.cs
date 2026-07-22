using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using LYBox.Platforms.Abstraction;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;
using LYBox.Layout.Core.Services;
using Ursa.Controls;

namespace LYBox.Layout.Ursa.Views;

public partial class MainWindow : UrsaWindow
{
    public WindowNotificationManager? NotificationManager { get; set; }

    /// <summary>
    /// 退出旁路标志：托盘菜单「退出」命令在调用 desktop.Shutdown() 前置为 true，
    /// 使关闭流程跳过「最小化到托盘」与退出确认对话框，允许窗口真正关闭。
    /// </summary>
    public static bool ForceExit { get; set; }

    /// <summary>
    /// 内部确认中标志：防止重复触发确认流程。
    /// </summary>
    private bool _isConfirming;

    public MainWindow()
    {
        InitializeComponent();

        NotificationManager = new WindowNotificationManager(this) { MaxItems = 3 };
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // 查找 WindowDrawnDecorations 中的所有关闭按钮（PART_CloseButton, PART_PopoverCloseButton），
        // 直接拦截其 Click 事件。UrsaWindow 的 WindowDrawnDecorations 关闭按钮可能不触发标准
        // Closing 事件或 CanClose，所以直接在按钮层面拦截。
        InterceptCloseButtons();
    }

    /// <summary>
    /// 在可视化树中查找所有关闭按钮并拦截 Click 事件。
    /// 关闭按钮通过 Name=PART_CloseButton / PART_PopoverCloseButton 标识。
    /// </summary>
    private void InterceptCloseButtons()
    {
        // 延迟一帧让模板应用完成
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            foreach (var btn in this.GetVisualDescendants())
            {
                if (btn is Button button && (button.Name == "PART_CloseButton" || button.Name == "PART_PopoverCloseButton"))
                {
                    // 先移除已有的 handler（防止重复）
                    button.Click -= OnCloseButtonClick;
                    button.Click += OnCloseButtonClick;
                }
            }
        });
    }

    /// <summary>
    /// 关闭按钮 Click 拦截：显示 Ursa OverlayMessageBox 确认对话框。
    /// </summary>
    private async void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        // 已在确认中或强制退出：不拦截
        if (_isConfirming || ForceExit)
            return;

        e.Handled = true;
        _isConfirming = true;

        try
        {
            // 设置驱动：开启「关闭时最小化到托盘」时隐藏窗口而非退出
            if (ServiceLocator.TryGetService<ISettingsService>(out var settings) && settings is not null)
            {
                var minimizeToTray = settings.GetValue<bool>("App.MinimizeToTray");
                if (minimizeToTray)
                {
                    this.Hide();
                    return;
                }
            }

            var loc = ServiceLocator.TryGetService<ILocalizationService>(out var service) ? service : null;
            var message = loc?.GetString("EXIT_CONFIRM_MESSAGE", "Are you sure you want to exit?") ?? "Are you sure you want to exit?";
            var title = loc?.GetString("EXIT_CONFIRM_TITLE", "Exit") ?? "Exit";

            // 使用 Ursa OverlayMessageBox（在 OverlayDialogHost 中显示，非独立窗口）
            var result = await OverlayMessageBox.ShowAsync(message, title, button: MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                // 用户确认退出：设置 ForceExit 并触发 Shutdown
                ForceExit = true;
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
            // 用户取消：什么都不做，窗口保持打开
        }
        finally
        {
            _isConfirming = false;
        }
    }

    /// <summary>
    /// 打开仓库链接（标题栏右侧 IconButton 触发）。
    /// </summary>
    private async void OpenRepository(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        var launcher = top.Launcher;
        await launcher.LaunchUriAsync(new Uri("https://github.com/irihitech/Ursa.Avalonia"));
    }
}
