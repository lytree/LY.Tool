using LYBox.Platforms.Abstraction.Services;

namespace LYBox.Platforms.Abstraction.Stubs.Services;

/// <inheritdoc />
public class SystemEventsServiceStub : ISystemEventsService
{
    /// <inheritdoc />
    public event EventHandler? TimeChanged;
}