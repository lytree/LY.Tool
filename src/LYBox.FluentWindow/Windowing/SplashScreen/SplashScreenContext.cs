using System.Threading;

namespace LYBox.FluentWindow.Windowing;

/// <summary>
/// Coordinates execution of an <see cref="IApplicationSplashScreen"/> against a host window.
/// </summary>
/// <remarks>
/// Simplified port from the AvaloniaFluentUI repository. The upstream
/// <c>AppSplashScreen</c> control dependency has been removed — the caller is
/// responsible for displaying <see cref="SplashScreen.SplashScreenContent"/> while
/// <see cref="RunJobs"/> is executing.
/// </remarks>
public class SplashScreenContext
{
    private readonly IApplicationSplashScreen _splashScreen;
    private CancellationTokenSource? _cts;

    public SplashScreenContext(IApplicationSplashScreen splashScreen)
    {
        _splashScreen = splashScreen;
    }

    /// <summary>The splash screen definition being coordinated.</summary>
    public IApplicationSplashScreen SplashScreen => _splashScreen;

    /// <summary>
    /// True after the splash has been shown at least once for this context.
    /// Hosts use this flag to avoid re-showing the splash on subsequent activations.
    /// </summary>
    public bool HasShownSplashScreen { get; set; }

    /// <summary>
    /// Runs the splash screen's startup tasks asynchronously. The splash should be
    /// visible for the duration of this call.
    /// </summary>
    public async Task RunJobs()
    {
        _cts = new CancellationTokenSource();
        try
        {
            await _splashScreen.RunTasks(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when TryCancel is invoked.
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// Requests cancellation of the running startup tasks (best-effort).
    /// </summary>
    public void TryCancel()
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // Swallow: cancellation is best-effort and may be invoked after the
            // tasks have already completed.
        }
    }
}
