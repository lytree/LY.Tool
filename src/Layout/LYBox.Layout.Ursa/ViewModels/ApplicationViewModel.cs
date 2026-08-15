using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LYBox.Layout.Core.Services;
using LYBox.Layout.Ursa.Views;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;


namespace LYBox.Layout.Ursa.ViewModels;

public partial class ApplicationViewModel : ObservableObject
{
    [RelayCommand]
    private void JumpTo(string header)
    {
        WeakReferenceMessenger.Default.Send(header, "JumpTo");
    }

    /// <summary>
    /// 从系统托盘显示/恢复主窗口。若主窗口已被关闭(理论上不会发生，最小化使用 Hide)，则重建。
    /// </summary>
    [RelayCommand]
    private void ShowMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.MainWindow;
            if (window is null)
            {
                window = new MainWindow
                {
                    DataContext = new MainWindowViewModel()
                };
                desktop.MainWindow = window;
                window.Show();
            }
            else
            {
                window.Show();
                window.WindowState = WindowState.Normal;
                window.Activate();
            }
        }
    }

    /// <summary>
    /// 从系统托盘退出应用。置位 ForceExit 以旁路 MainWindow.CanClose() 的最小化/确认逻辑，
    /// 随后触发桌面生命周期的 Shutdown。
    /// </summary>
    [RelayCommand]
    private void ExitApplication()
    {
        MainWindow.ForceExit = true;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
