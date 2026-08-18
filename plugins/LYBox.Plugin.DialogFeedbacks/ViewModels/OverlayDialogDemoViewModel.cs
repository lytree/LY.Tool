using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DialogFeedbacks.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Ursa.Controls;
using LYBox.Plugin.DialogFeedbacks.Dialogs;


namespace LYBox.Plugin.DialogFeedbacks.ViewModels;

[NavigationItem("KeyOverlayDialog")]
[Menu("NAV_OverlayDialog", "KeyOverlayDialog", "NAV_DialogFeedbacks")]
[ViewMap(typeof(OverlayDialogDemo))]
public partial class OverlayDialogDemoViewModel : ObservableObject
{
    public const string LocalHost = "LocalHost";
    public DefaultOverlayDialogDemoViewModel DefaultOverlayDialogDemoViewModel { get; set; } = new();
    public CustomOverlayDialogDemoViewModel CustomOverlayDialogDemoViewModel { get; set; } = new();
}

public partial class DefaultOverlayDialogDemoViewModel : ObservableObject
{
    [ObservableProperty] private HorizontalPosition _horizontalAnchor;
    [ObservableProperty] private VerticalPosition _verticalAnchor;
    [ObservableProperty] private double? _horizontalOffset;
    [ObservableProperty] private double? _verticalOffset;
    [ObservableProperty] private bool _fullScreen;
    [ObservableProperty] private DialogMode _mode;
    [ObservableProperty] private DialogButton _button;
    [ObservableProperty] private string? _title;
    [ObservableProperty] private bool _canLightDismiss;
    [ObservableProperty] private bool _canDragMove;
    [ObservableProperty] private bool? _isCloseButtonVisible;
    [ObservableProperty] private bool _isModal;
    [ObservableProperty] private bool _isLocal;
    [ObservableProperty] private bool _canResize;
    [ObservableProperty] private string? _styleClass;

    public ICommand ShowDialogCommand { get; }

    public DefaultOverlayDialogDemoViewModel()
    {
        ShowDialogCommand = new AsyncRelayCommand(ShowDialog);
        HorizontalAnchor = HorizontalPosition.Center;
        VerticalAnchor = VerticalPosition.Center;
        CanDragMove = true;
        IsModal = true;
        IsCloseButtonVisible = true;
        Button = DialogButton.OKCancel;
    }

    private async Task ShowDialog()
    {
        var options = new OverlayDialogOptions()
        {
            FullScreen = FullScreen,
            HorizontalAnchor = HorizontalAnchor,
            VerticalAnchor = VerticalAnchor,
            HorizontalOffset = HorizontalOffset,
            VerticalOffset = VerticalOffset,
            Mode = Mode,
            Buttons = Button,
            Title = Title,
            CanLightDismiss = CanLightDismiss,
            CanDragMove = CanDragMove,
            IsCloseButtonVisible = IsCloseButtonVisible,
            CanResize = CanResize,
            StyleClass = StyleClass,
        };
        string? dialogHostId = IsLocal ? OverlayDialogDemoViewModel.LocalHost : null;
        if (IsModal)
        {
            await OverlayDialog.ShowStandardAsync<DefaultDemoDialog, DefaultDemoDialogViewModel>(new DefaultDemoDialogViewModel(), dialogHostId, options: options);
        }
        else
        {
            OverlayDialog.ShowStandard<DefaultDemoDialog, DefaultDemoDialogViewModel>(new DefaultDemoDialogViewModel(), dialogHostId, options: options);
        }
    }
}

public partial class CustomOverlayDialogDemoViewModel : ObservableObject
{
    [ObservableProperty] private HorizontalPosition _horizontalAnchor;
    [ObservableProperty] private VerticalPosition _verticalAnchor;
    [ObservableProperty] private double? _horizontalOffset;
    [ObservableProperty] private double? _verticalOffset;
    [ObservableProperty] private bool _fullScreen;
    [ObservableProperty] private string? _title;
    [ObservableProperty] private bool _canLightDismiss;
    [ObservableProperty] private bool _canDragMove;
    [ObservableProperty] private bool? _isCloseButtonVisible;
    [ObservableProperty] private bool _isModal;
    [ObservableProperty] private bool _isLocal;
    [ObservableProperty] private bool _canResize;

    public ICommand ShowDialogCommand { get; }

    public CustomOverlayDialogDemoViewModel()
    {
        ShowDialogCommand = new AsyncRelayCommand(ShowDialog);
        HorizontalAnchor = HorizontalPosition.Center;
        VerticalAnchor = VerticalPosition.Center;
        CanDragMove = true;
        IsModal = true;
    }

    private async Task ShowDialog()
    {
        var options = new OverlayDialogOptions()
        {
            FullScreen = FullScreen,
            HorizontalAnchor = HorizontalAnchor,
            VerticalAnchor = VerticalAnchor,
            HorizontalOffset = HorizontalOffset,
            VerticalOffset = VerticalOffset,
            Title = Title,
            CanLightDismiss = CanLightDismiss,
            CanDragMove = CanDragMove,
            IsCloseButtonVisible = IsCloseButtonVisible,
            CanResize = CanResize,
        };
        var dialogHostId = IsLocal ? OverlayDialogDemoViewModel.LocalHost : null;
        if (IsModal)
        {
            await OverlayDialog.ShowCustomAsync<CustomDemoDialog, CustomDemoDialogViewModel, object>(new CustomDemoDialogViewModel(), dialogHostId, options: options);
        }
        else
        {
            OverlayDialog.ShowCustom<CustomDemoDialog, CustomDemoDialogViewModel>(new CustomDemoDialogViewModel(), dialogHostId, options: options);
        }
    }
}
