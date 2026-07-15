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
        // 按平台应用 chrome 策略：
        // - Windows/macOS: BorderOnly + ExtendClientArea=false（移除标题栏，保留 resize frame）
        // - Linux: BorderOnly + ExtendClientArea=true + NoChrome（覆盖原生标题栏，保留 WM resize）
        // 标题栏职责由 MainView 工具栏自绘承担。
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
