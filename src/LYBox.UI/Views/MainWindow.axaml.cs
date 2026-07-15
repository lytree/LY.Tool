using System.Threading.Tasks;
using LYBox.Platforms.Abstraction;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;
using LYBox.UI.Services;
using Ursa.Controls;

namespace LYBox.UI.Views;

public partial class MainWindow : UrsaWindow
{
    public WindowNotificationManager? NotificationManager { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        // 按平台应用 chrome 策略：Windows/macOS 走扩展客户区 + WindowDrawnDecorations；
        // Linux 回退到 BorderOnly + 应用层自绘 FluentTitleBar。
        PlatformServices.WindowChromeService.ApplyChrome(this);
        NotificationManager = new WindowNotificationManager(this) { MaxItems = 3 };

        if (ServiceLocator.TryGetService<IWindowInfoService>(out var windowInfoService) && windowInfoService is WindowInfoService impl)
        {
            impl.Initialize(this);
        }
    }

    protected override async Task<bool> CanClose()
    {
        var loc = ServiceLocator.TryGetService<ILocalizationService>(out var service) ? service : null;
        var message = loc?.GetString("EXIT_CONFIRM_MESSAGE", "Are you sure you want to exit?") ?? "Are you sure you want to exit?";
        var title = loc?.GetString("EXIT_CONFIRM_TITLE", "Exit") ?? "Exit";
        var result = await OverlayMessageBox.ShowAsync(message, title, button: MessageBoxButton.YesNo);
        return result == MessageBoxResult.Yes;
    }
}
