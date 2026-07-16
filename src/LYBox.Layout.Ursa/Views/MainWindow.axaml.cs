using System.Threading.Tasks;
using LYBox.Platforms.Abstraction;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;
using LYBox.Layout.Core.Services;
using Ursa.Controls;

namespace LYBox.Layout.Ursa.Views;

public partial class MainWindow : global::Ursa.Controls.UrsaWindow
{
    public WindowNotificationManager? NotificationManager { get; set; }

    public MainWindow()
    {
        InitializeComponent();

        NotificationManager = new WindowNotificationManager(this) { MaxItems = 3 };
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
