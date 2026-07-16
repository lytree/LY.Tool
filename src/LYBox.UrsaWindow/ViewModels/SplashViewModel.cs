using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Irihi.Avalonia.Shared.Contracts;

namespace LYBox.UrsaWindow.ViewModels;

public partial class SplashViewModel: ObservableObject, IDialogContext
{
    [ObservableProperty] private double _progress;
    private Random _r = new();
    private IDisposable? _timerDisposable;

    public SplashViewModel()
    {
        _timerDisposable = DispatcherTimer.Run(OnUpdate, TimeSpan.FromMilliseconds(20), DispatcherPriority.Default);
    }

    private bool OnUpdate()
    {
        if (Progress >= 100)
        {
            _timerDisposable?.Dispose();
            _timerDisposable = null;
            RequestClose?.Invoke(this, true);
            return false;
        }
        Progress = Math.Min(100, Progress + 10 * _r.NextDouble());
        return true;
    }
    
    public void Close()
    {
        _timerDisposable?.Dispose();
        _timerDisposable = null;
        RequestClose?.Invoke(this, false);
    }

    public event EventHandler<object?>? RequestClose;
}
