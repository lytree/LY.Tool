using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.DialogFeedbacks.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Ursa.Common;
using Ursa.Controls;
using Ursa.Controls.Options;
using Avalonia.Plugin.Shared.Dialogs;


namespace Avalonia.Plugin.DialogFeedbacks.ViewModels;

[NavigationItem("KeyDrawer")]
[Menu("Drawer", "KeyDrawer", "Dialog & Feedbacks")]
[ViewMap(typeof(DrawerDemo))]
public partial class DrawerDemoViewModel : ObservableObject
{
    public ICommand ShowDialogCommand { get; set; }

    [ObservableProperty] private Position _position;
    [ObservableProperty] private DialogButton _buttons;

    [ObservableProperty] private bool _canLightDismiss;
    [ObservableProperty] private bool _isModal;
    [ObservableProperty] private bool? _isCloseButtonVisible;
    [ObservableProperty] private string? _title;

    [ObservableProperty] private bool _custom;
    [ObservableProperty] private bool _isLocal;
    [ObservableProperty] private bool _canResize;

    public DrawerDemoViewModel()
    {
        ShowDialogCommand = new AsyncRelayCommand(ShowDefaultDialog);
        Position = Position.Right;
        IsModal = true;
        Title = "Add New";
    }

    private async Task ShowDefaultDialog()
    {
        var options = new DrawerOptions()
        {
            Position = Position,
            Buttons = Buttons,
            CanLightDismiss = CanLightDismiss,
            IsCloseButtonVisible = IsCloseButtonVisible,
            Title = Title,
            CanResize = CanResize,
        };
        var hostId = IsLocal ? "LocalHost" : null;
        if (Custom)
        {
            var vm = new CustomDemoDialogViewModel();
            if (IsModal)
            {
                await OverlayDrawer.ShowCustomAsync<CustomDemoDialog, CustomDemoDialogViewModel, object?>(vm, hostId, options);
            }
            else
            {
                OverlayDrawer.ShowCustom<CustomDemoDialog, CustomDemoDialogViewModel>(vm, hostId, options);
            }
        }
        else
        {
            var vm = new DefaultDemoDialogViewModel();
            if (IsModal)
            {
                await OverlayDrawer.ShowStandardAsync<DefaultDemoDialog, DefaultDemoDialogViewModel>(vm, hostId, options);
            }
            else
            {
                OverlayDrawer.ShowStandard<DefaultDemoDialog, DefaultDemoDialogViewModel>(vm, hostId, options);
            }
        }
        
    }
}





