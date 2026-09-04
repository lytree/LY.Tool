namespace LYBox.Plugin.Downloader.Services;

public class DirectLogger
{
    private readonly Action<string> _onLog;

    public DirectLogger(Action<string> onLog)
    {
        _onLog = onLog;
    }

    public void Log(string message) => _onLog(message);
}
