using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace LYBox.FluentWindow.Views;

public partial class DemoPage : UserControl
{
    private LYBox.FluentWindow.Windowing.FluentWindow? _parentWindow;

    public DemoPage()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _parentWindow = this.FindAncestorOfType<LYBox.FluentWindow.Windowing.FluentWindow>();
    }

    private void ToggleTitleBar_Click(object? sender, RoutedEventArgs e)
    {
        if (_parentWindow is not null)
        {
            _parentWindow.TitleBarIsVisible = !_parentWindow.TitleBarIsVisible;
        }
    }

    private void ToggleCaption_Click(object? sender, RoutedEventArgs e)
    {
        if (_parentWindow is not null)
        {
            _parentWindow.MinButtonIsVisible = !_parentWindow.MinButtonIsVisible;
            _parentWindow.MaxButtonIsVisible = !_parentWindow.MaxButtonIsVisible;
            _parentWindow.CloseButtonIsVisible = !_parentWindow.CloseButtonIsVisible;
        }
    }
}
