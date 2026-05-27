using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.Shared;

public class ViewModelBase : ObservableObject, IDisposable
{
    private bool _disposed;

    public bool IsDisposed => _disposed;

    public virtual void Dispose()
    {
        _disposed = true;
    }
}

