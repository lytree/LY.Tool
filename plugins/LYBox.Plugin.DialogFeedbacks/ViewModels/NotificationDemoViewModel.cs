using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DialogFeedbacks.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.Input;
using Notification = Ursa.Controls.Notification;
using WindowNotificationManager = Ursa.Controls.WindowNotificationManager;

namespace LYBox.Plugin.DialogFeedbacks.ViewModels;

[NavigationItem("KeyNotification")]
[Menu("NAV_Notification", "KeyNotification", "NAV_DialogFeedbacks")]
[ViewMap(typeof(NotificationDemo))]
public partial class NotificationDemoViewModel : ObservableObject
{
    public WindowNotificationManager? NotificationManager { get; set; }

    [ObservableProperty] private bool _showIcon = true;
    [ObservableProperty] private bool _showClose = true;
    [ObservableProperty] private Ursa.Controls.MessageCloseReason? _reason;

    [RelayCommand]
    public void ChangePosition(object obj)
    {
        if (obj is string s && NotificationManager is not null)
        {
            Enum.TryParse<NotificationPosition>(s, out var notificationPosition);
            NotificationManager.Position = notificationPosition;
        }
    }

    [RelayCommand]
    public void ShowNormal(object obj)
    {
        if (obj is not string s) return;
        Enum.TryParse<NotificationType>(s, out var notificationType);
        NotificationManager?.Show(
            new Notification("Welcome", "This is message"),
            showIcon: ShowIcon,
            showClose: ShowClose,
            type: notificationType,
            onClose: OnClose);
    }

    [RelayCommand]
    public void ShowLight(object obj)
    {
        if (obj is not string s) return;
        Enum.TryParse<NotificationType>(s, out var notificationType);
        NotificationManager?.Show(
            new Notification("Welcome", "This is message"),
            showIcon: ShowIcon,
            showClose: ShowClose,
            type: notificationType,
            onClose: OnClose,
            classes: ["Light"]);
    }

    private void OnClose(Ursa.Controls.MessageCloseReason reason)
    {
        Reason = reason;
    }
}





