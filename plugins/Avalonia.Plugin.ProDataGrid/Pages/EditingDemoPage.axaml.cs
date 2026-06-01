using Avalonia.Controls;
using Avalonia.Plugin.ProDataGrid.ViewModels;

namespace Avalonia.Plugin.ProDataGrid.Pages;

public partial class EditingDemoPage : UserControl
{
    public EditingDemoPage()
    {
        InitializeComponent();
    }

    private EditingDemoViewModel? Vm => DataContext as EditingDemoViewModel;

    private void OnBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        var header = e.Column.Header?.ToString() ?? "?";
        Vm?.AddLog($"BeginningEdit: 行{e.Row.Index} 列[{header}] 触发={e.EditingEventArgs?.GetType().Name ?? "程序"}");
    }

    private void OnPreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
    {
        var header = e.Column.Header?.ToString() ?? "?";
        var elementType = e.EditingElement?.GetType().Name ?? "null";
        Vm?.AddLog($"PreparingCellForEdit: 行{e.Row.Index} 列[{header}] 编辑控件={elementType}");
    }

    private void OnCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        var header = e.Column.Header?.ToString() ?? "?";
        Vm?.AddLog($"CellEditEnding: 行{e.Row.Index} 列[{header}] 操作={e.EditAction} 取消={e.Cancel}");
    }

    private void OnCellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
    {
        var header = e.Column.Header?.ToString() ?? "?";
        Vm?.AddLog($"CellEditEnded: 行{e.Row.Index} 列[{header}] 操作={e.EditAction}");
    }
}
