using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using Avalonia.Plugin.Downloader.Models;
using Avalonia.Plugin.Downloader.Services;
using Avalonia.Plugin.Shared;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogEntry = Avalonia.Plugin.Downloader.Models.LogEntry;

namespace Avalonia.Plugin.Downloader.ViewModels;

public abstract partial class DownloaderViewModelBase : ViewModelBase
{
    public abstract ScriptDescriptor Script { get; }

    [ObservableProperty] private ObservableCollection<ScriptParameter> _parameters = [];
    [ObservableProperty] private ObservableCollection<LogEntry> _logEntries = [];
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = "就绪";

    private string _logText = string.Empty;
    private readonly StringBuilder _logBuilder = new();
    private bool _logTextDirty;
    private DispatcherTimer? _logUpdateTimer;

    public string LogText => _logText;

    partial void OnLogEntriesChanged(ObservableCollection<LogEntry> value)
    {
        value.CollectionChanged += OnLogEntriesCollectionChanged;
        RebuildLogText();
        FlushLogUpdate();
    }

    private CancellationTokenSource? _cts;

    protected DownloaderViewModelBase()
    {
        LogEntries.CollectionChanged += OnLogEntriesCollectionChanged;

        foreach (var param in Script.Parameters)
        {
            Parameters.Add(param);
        }
    }

    private void OnLogEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems?.Count == 1)
        {
            AppendLogText((LogEntry)e.NewItems[0]!);
        }
        else
        {
            RebuildLogText();
        }
        ScheduleLogUpdate();
    }

    private void AppendLogText(LogEntry entry)
    {
        var line = $"[{entry.Timestamp:HH:mm:ss}] {entry.Message}";
        if (_logBuilder.Length > 0)
            _logBuilder.AppendLine();
        _logBuilder.Append(line);
        _logText = _logBuilder.ToString();
    }

    private void RebuildLogText()
    {
        _logBuilder.Clear();
        if (LogEntries.Count > 0)
        {
            _logBuilder.Append(string.Join(Environment.NewLine, LogEntries.Select(e => $"[{e.Timestamp:HH:mm:ss}] {e.Message}")));
        }
        _logText = _logBuilder.ToString();
    }

    private void ScheduleLogUpdate()
    {
        _logTextDirty = true;
        if (_logUpdateTimer == null)
        {
            _logUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _logUpdateTimer.Tick += OnLogUpdateTimerTick;
        }
        if (!_logUpdateTimer.IsEnabled)
            _logUpdateTimer.Start();
    }

    private void FlushLogUpdate()
    {
        _logUpdateTimer?.Stop();
        if (_logTextDirty)
        {
            _logTextDirty = false;
            OnPropertyChanged(nameof(LogText));
        }
    }

    private void OnLogUpdateTimerTick(object? sender, EventArgs e)
    {
        _logUpdateTimer!.Stop();
        if (_logTextDirty)
        {
            _logTextDirty = false;
            OnPropertyChanged(nameof(LogText));
        }
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogEntries.Clear();
        _logBuilder.Clear();
        _logText = string.Empty;
        _logTextDirty = false;
        _logUpdateTimer?.Stop();
        OnPropertyChanged(nameof(LogText));
    }

    [RelayCommand]
    private async Task ExecuteScript()
    {
        if (IsRunning) return;

        IsRunning = true;
        StatusText = $"正在执行: {Script.Name}...";
        _cts = new CancellationTokenSource();

        try
        {
            var paramValues = BuildParameterValues();
            await ExecuteCoreAsync(paramValues, _cts.Token);
            StatusText = "执行完成";
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消";
        }
        catch (Exception ex)
        {
            AddLogEntry(new LogEntry { Message = $"执行失败: {ex.Message}" });
            StatusText = "执行失败";
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void CancelExecution()
    {
        _cts?.Cancel();
        StatusText = "正在取消...";
    }

    protected abstract Task ExecuteCoreAsync(Dictionary<string, string> paramValues, CancellationToken ct);

    protected DirectUiLogger CreateUiLogger()
    {
        return new DirectUiLogger(message => AddLogEntry(new LogEntry { Message = message }));
    }

    protected Dictionary<string, string> BuildParameterValues()
    {
        var values = new Dictionary<string, string>();
        foreach (var param in Parameters)
        {
            values[param.Key] = param.DefaultValue ?? string.Empty;
        }
        return values;
    }

    protected void AddLogEntry(LogEntry entry)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogEntries.Add(entry);
            if (LogEntries.Count > 1000)
            {
                LogEntries.RemoveAt(0);
            }
        });
    }
}
