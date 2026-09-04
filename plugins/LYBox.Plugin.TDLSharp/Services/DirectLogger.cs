using LYBox.Plugin.TDLSharp.Models;

namespace LYBox.Plugin.TDLSharp.Services;

public class DirectLogger
{
    private readonly Action<string> _onLog;
    private readonly Action<LogEntry> _onAddEntry;
    private readonly Action<LogEntry, double, string, bool, bool> _onUpdateProgress;

    public DirectLogger(
        Action<string> onLog,
        Action<LogEntry> onAddEntry,
        Action<LogEntry, double, string, bool, bool> onUpdateProgress)
    {
        _onLog = onLog;
        _onAddEntry = onAddEntry;
        _onUpdateProgress = onUpdateProgress;
    }

    public void Log(string message) => _onLog(message);

    public LogEntry StartProgress(string fileName, long fileSize, string initialStatus)
    {
        var entry = new LogEntry
        {
            IsProgress = true,
            FileName = fileName,
            FileSize = fileSize,
            StatusText = initialStatus
        };
        _onAddEntry(entry);
        return entry;
    }

    public void UpdateProgress(LogEntry entry, double progressValue, string status)
    {
        _onUpdateProgress(entry, progressValue, status, false, false);
    }

    public void CompleteProgress(LogEntry entry, string status)
    {
        _onUpdateProgress(entry, 100, status, true, false);
    }

    public void FailProgress(LogEntry entry, string status)
    {
        _onUpdateProgress(entry, entry.ProgressValue, status, false, true);
    }
}
