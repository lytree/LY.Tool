using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Plugin.TDLSharp.ViewModels;

namespace Avalonia.Plugin.TDLSharp.Controls;

public partial class ExecutionHistoryDialog : UserControl
{
    public ExecutionHistoryDialog()
    {
        InitializeComponent();
    }

    private void DataGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem is not null)
        {
            if (DataContext is ExecutionHistoryDialogViewModel vm)
            {
                vm.ApplyParametersCommand.Execute(grid.SelectedItem);
            }
        }
    }
}
