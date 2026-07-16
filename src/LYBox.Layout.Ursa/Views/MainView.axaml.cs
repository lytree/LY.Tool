using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;
using Ursa.Controls;
using LYBox.Layout.Ursa.ViewModels;

namespace LYBox.Layout.Ursa.Views;

public partial class MainView : UserControl
{
    private MainViewViewModel? _viewModel;
    private Window? _window;

    public MainView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _viewModel = DataContext as MainViewViewModel;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || _viewModel is null)
            return;
        _viewModel.NotificationManager = WindowNotificationManager.TryGetNotificationManager(topLevel, out var manager)
            ? manager
            : new WindowNotificationManager(topLevel);
    }

}
