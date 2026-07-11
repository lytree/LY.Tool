using System.Threading.Tasks;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;
using LYBox.UI.Services;
using Ursa.Controls;

namespace LYBox.UI.Views;

public partial class MainWindow : UrsaWindow
{
    public WindowNotificationManager? NotificationManager { get; set; }

    /// <summary>
    /// 退出旁路标志：托盘菜单「退出」命令在调用 desktop.Shutdown() 前置为 true，
    /// 使 CanClose() 跳过「最小化到托盘」与退出确认对话框，允许窗口真正关闭。
    /// </summary>
    public static bool ForceExit { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        NotificationManager = new WindowNotificationManager(this) { MaxItems = 3 };

        if (ServiceLocator.TryGetService<IWindowInfoService>(out var windowInfoService) && windowInfoService is WindowInfoService impl)
        {
            impl.Initialize(this);
        }
    }

    protected override async Task<bool> CanClose()
    {
        if (ForceExit) return true;

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

        var loc = ServiceLocator.TryGetService<ILocalizationService>(out var service) ? service : null;
        var message = loc?.GetString("EXIT_CONFIRM_MESSAGE", "Are you sure you want to exit?") ?? "Are you sure you want to exit?";
        var title = loc?.GetString("EXIT_CONFIRM_TITLE", "Exit") ?? "Exit";
        var result = await OverlayMessageBox.ShowAsync(message, title, button: MessageBoxButton.YesNo);
        return result == MessageBoxResult.Yes;
    }
}
