using Avalonia.Controls;
using Avalonia.Plugin.ProDataGrid.CustomDrawing;
using Avalonia.Plugin.ProDataGrid.ViewModels;

namespace Avalonia.Plugin.ProDataGrid.Pages;

public partial class CustomDrawingLiveUpdatesPage : UserControl
{
    public CustomDrawingLiveUpdatesPage()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext is CustomDrawingLiveUpdatesViewModel vm)
        {
            var factory = this.TryFindResource("LiveSkiaFactory", out var raw) && raw is SkiaAnimatedTextCellDrawOperationFactory f
                ? f
                : null;
            vm.AttachFactory(factory);
            vm.OnAttached();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (DataContext is CustomDrawingLiveUpdatesViewModel vm)
        {
            vm.OnDetached();
        }
    }
}
