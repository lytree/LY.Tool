using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Layout.Ursa.Dialogs;

/// <summary>
/// 退出确认对话框 ViewModel。
/// </summary>
public partial class ExitConfirmDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _message;

    public ExitConfirmDialogViewModel(string message)
    {
        _message = message;
    }
}
