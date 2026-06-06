using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Plugin.TDLSharp.ViewModels;

namespace Avalonia.Plugin.TDLSharp.Controls;

public partial class ExecutionHistoryDialog : UserControl
{
    public ExecutionHistoryDialog()
    {
        InitializeComponent();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            Width = window.Bounds.Width * 0.6;
            Height = window.Bounds.Height * 0.6;
        }
    }

    private void DataGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is DataGrid dataGrid && dataGrid.SelectedItem is not null)
        {
            if (DataContext is ExecutionHistoryDialogViewModel vm)
            {
                vm.ApplyParametersCommand.Execute(dataGrid.SelectedItem);
            }
        }
    }
}
