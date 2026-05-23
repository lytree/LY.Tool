using Avalonia.Controls;
using Avalonia.Plugin.Downloader.ViewModels;
using Avalonia.Threading;
using System.ComponentModel;

namespace Avalonia.Plugin.Downloader.Pages;

public partial class M3u8DownloaderPage : UserControl
{
    private DownloaderViewModelBase? _currentVm;

    public M3u8DownloaderPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_currentVm is not null)
        {
            _currentVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _currentVm = DataContext as DownloaderViewModelBase;

        if (_currentVm is not null)
        {
            _currentVm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DownloaderViewModelBase.LogText))
        {
            Dispatcher.UIThread.Post(() =>
            {
                LogScrollViewer.ScrollToEnd();
            }, DispatcherPriority.Loaded);
        }
    }
}
