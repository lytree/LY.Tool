using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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

    public MainWindow()
    {
        InitializeComponent();

        NotificationManager = new WindowNotificationManager(this) { MaxItems = 3 };
    }

    protected override async Task<bool> CanClose()
    {
        // 强制退出（托盘菜单退出路径）：直接放行
        if (ForceExit)
            return true;

        // 设置驱动：开启「关闭时最小化到托盘」时隐藏窗口而非退出
        if (ServiceLocator.TryGetService<ISettingsService>(out var settings) && settings is not null)
        {
            var minimizeToTray = settings.GetValue<bool>("App.MinimizeToTray");
            if (minimizeToTray)
            {
                this.Hide();
                return false;
            }
        }

        // 本地化文案
        var loc = ServiceLocator.TryGetService<ILocalizationService>(out var service) ? service : null;
        var message = loc?.GetString("EXIT_CONFIRM_MESSAGE", "Are you sure you want to exit?") ?? "Are you sure you want to exit?";
        var title = loc?.GetString("EXIT_CONFIRM_TITLE", "Exit") ?? "Exit";

        // Ursa 标准 Overlay Dialog 确认（参照 Ursa.Demo MainWindow 实现）
        var result = await OverlayMessageBox.ShowAsync(message, title, button: MessageBoxButton.YesNo);
        if (result == MessageBoxResult.Yes)
        {
            // 用户确认退出：标记 ForceExit 并触发 Shutdown，绕过下次 CanClose 拦截
            ForceExit = true;
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }
        return false;
    }
}
