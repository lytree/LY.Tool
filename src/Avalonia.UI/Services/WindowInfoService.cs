using Avalonia.Controls;
using Avalonia.Plugin.Shared.Services;
using CommunityToolkit.Mvvm.Messaging;

namespace Avalonia.UI.Services;

public class WindowInfoService : IWindowInfoService
{
    private Window? _window;

    public double Width => _window?.Bounds.Width ?? 0;
    public double Height => _window?.Bounds.Height ?? 0;

    public void Initialize(Window window)
    {
        if (_window is not null)
        {
            _window.Resized -= OnResized;
        }

        _window = window;
        _window.Resized += OnResized;
    }

    private void OnResized(object? sender, WindowResizedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new WindowSizeChangedMessage(e.ClientSize.Width, e.ClientSize.Height));
    }
}
