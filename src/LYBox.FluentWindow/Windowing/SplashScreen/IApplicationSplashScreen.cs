namespace LYBox.FluentWindow.Windowing;

/// <summary>
/// Provides the content shown during application startup and an opportunity to run
/// background initialization tasks while the splash screen is visible.
/// </summary>
/// <remarks>
/// Ported from the AvaloniaFluentUI repository. The interface is intentionally minimal:
/// the host renders <see cref="SplashScreenContent"/> while <see cref="RunTasks"/> is
/// executing and keeps it on screen for at least <see cref="MinimumShowTime"/> milliseconds.
/// </remarks>
public interface IApplicationSplashScreen
{
    /// <summary>
    /// The content (typically an Avalonia control/view) to render while the splash
    /// screen is visible.
    /// </summary>
    object SplashScreenContent { get; }

    /// <summary>
    /// Runs any startup tasks. The host keeps the splash visible until this completes
    /// or the supplied cancellation token is cancelled.
    /// </summary>
    Task RunTasks(CancellationToken token);

    /// <summary>
    /// Minimum number of milliseconds the splash screen should remain visible, even
    /// if <see cref="RunTasks"/> completes earlier.
    /// </summary>
    int MinimumShowTime { get; }
}
