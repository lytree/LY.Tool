using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DialogFeedbacks.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.Input;
using Ursa.Controls;

namespace LYBox.Plugin.DialogFeedbacks.ViewModels;

[NavigationItem("KeyToast")]
[Menu("NAV_Toast", "KeyToast", "NAV_DialogFeedbacks")]
[ViewMap(typeof(ToastDemo))]
public partial class ToastDemoViewModel : ObservableObject
{
    public WindowToastManager? ToastManager { get; set; }

    [ObservableProperty] private bool _showIcon = true;
    [ObservableProperty] private bool _showClose = true;
    [ObservableProperty] private Ursa.Controls.MessageCloseReason? _reason;

    [RelayCommand]
    public void ShowNormal(object obj)
    {
        if (obj is string s)
        {
            Enum.TryParse<NotificationType>(s, out var notificationType);
            ToastManager?.Show(
                new Toast("This is message"),
                showIcon: ShowIcon,
                showClose: ShowClose,
                type: notificationType,
                onClose: OnClose);
        }

        // ToastManager?.Show(new ToastDemoViewModel
        // {
        //     Content = "This is message",
        //     ToastManager = ToastManager
        // });
    }

    [RelayCommand]
    public void ShowLight(object obj)
    {
        if (obj is string s)
        {
            Enum.TryParse<NotificationType>(s, out var notificationType);
            ToastManager?.Show(
                new Toast("This is message"),
                showIcon: ShowIcon,
                showClose: ShowClose,
                type: notificationType,
                onClose: OnClose,
                classes: ["Light"]);
        }
    }

    private void OnClose(Ursa.Controls.MessageCloseReason reason)
    {
        Reason = reason;
    }

    public string? Content { get; set; }

    [RelayCommand]
    public void YesCommand()
    {
        ToastManager?.Show(new Toast("Yes!"));
    }

    [RelayCommand]
    public void NoCommand()
    {
        ToastManager?.Show(new Toast("No!"));
    }
}





