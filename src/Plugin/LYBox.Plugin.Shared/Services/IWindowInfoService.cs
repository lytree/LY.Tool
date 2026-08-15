using CommunityToolkit.Mvvm.Messaging.Messages;

namespace LYBox.Plugin.Shared.Services;

public interface IWindowInfoService
{
    double Width { get; }
    double Height { get; }
}

public sealed class WindowSizeChangedMessage(double width, double height) : ValueChangedMessage<WindowSize>(new WindowSize(width, height))
{
}

public sealed class WindowSize(double width, double height)
{
    public double Width { get; } = width;
    public double Height { get; } = height;
}
