using Avalonia.Controls;
using Avalonia.Plugin.ProDataGrid.Models;
using Avalonia.Plugin.ProDataGrid.ViewModels;

namespace Avalonia.Plugin.ProDataGrid.Pages;

public partial class EditingDemoPage : UserControl
{
    private EditingDemoRow? _editingRow;
    private string? _editingPropertyName;
    private Dictionary<string, object?> _beforeEditSnapshot = [];

    private static readonly Dictionary<string, string> ColumnBindingMap = new()
    {
        ["产品"] = nameof(EditingDemoRow.Product),
        ["分类"] = nameof(EditingDemoRow.Category),
        ["单价"] = nameof(EditingDemoRow.Price),
        ["数量"] = nameof(EditingDemoRow.Quantity),
        ["有货"] = nameof(EditingDemoRow.InStock),
        ["供应商"] = nameof(EditingDemoRow.Supplier),
        ["备注"] = nameof(EditingDemoRow.Notes)
    };

    public EditingDemoPage()
    {
        InitializeComponent();
    }

    private EditingDemoViewModel? Vm => DataContext as EditingDemoViewModel;

    private void OnBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        var header = e.Column.Header?.ToString() ?? "?";
        Vm?.AddLog($"BeginningEdit: 行{e.Row.Index} 列[{header}] 触发={e.EditingEventArgs?.GetType().Name ?? "程序"}");

        if (e.Row.DataContext is EditingDemoRow row && ColumnBindingMap.TryGetValue(header, out var propName))
        {
            _editingRow = row;
            _editingPropertyName = propName;
            _beforeEditSnapshot[propName] = GetPropertyValue(row, propName);
        }
        else
        {
            _editingRow = null;
            _editingPropertyName = null;
        }
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

        if (e.EditAction == DataGridEditAction.Commit &&
            _editingRow is not null &&
            _editingPropertyName is not null &&
            ColumnBindingMap.TryGetValue(header, out var propName))
        {
            var oldValue = _beforeEditSnapshot.GetValueOrDefault(propName);
            var newValue = GetPropertyValue(_editingRow, propName);

            if (!Equals(oldValue, newValue))
            {
                Vm?.RecordCellEdit(_editingRow, propName, oldValue, newValue);
            }
        }

        _editingRow = null;
        _editingPropertyName = null;
        _beforeEditSnapshot.Clear();
    }

    private static object? GetPropertyValue(EditingDemoRow row, string propertyName)
    {
        return propertyName switch
        {
            nameof(EditingDemoRow.Product) => row.Product,
            nameof(EditingDemoRow.Category) => row.Category,
            nameof(EditingDemoRow.Price) => row.Price,
            nameof(EditingDemoRow.Quantity) => row.Quantity,
            nameof(EditingDemoRow.InStock) => row.InStock,
            nameof(EditingDemoRow.Supplier) => row.Supplier,
            nameof(EditingDemoRow.Notes) => row.Notes,
            _ => null
        };
    }
}
