using System.Threading.Tasks;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Services;
using Avalonia.UI.Services;
using Ursa.Controls;

namespace Avalonia.UI.Views;

public partial class MainWindow : UrsaWindow
{
    public WindowNotificationManager? NotificationManager { get; set; }

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
        var loc = ServiceLocator.TryGetService<ILocalizationService>(out var service) ? service : null;
        var message = loc?.GetString("EXIT_CONFIRM_MESSAGE", "Are you sure you want to exit?") ?? "Are you sure you want to exit?";
        var title = loc?.GetString("EXIT_CONFIRM_TITLE", "Exit") ?? "Exit";
        var result = await OverlayMessageBox.ShowAsync(message, title, button: MessageBoxButton.YesNo);
        return result == MessageBoxResult.Yes;
    }
}
