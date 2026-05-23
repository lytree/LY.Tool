namespace Avalonia.Plugin.Downloader.Services;

public class DirectUiLogger
{
    private readonly Action<string> _onLog;

    public DirectUiLogger(Action<string> onLog)
    {
        _onLog = onLog;
    }

    public void Log(string message) => _onLog(message);
}
