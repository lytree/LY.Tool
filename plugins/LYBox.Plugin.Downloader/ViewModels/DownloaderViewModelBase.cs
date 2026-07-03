using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Input.Platform;
using LYBox.Plugin.Downloader.Models;
using LYBox.Plugin.Downloader.Resources;
using LYBox.Plugin.Downloader.Services;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace LYBox.Plugin.Downloader.ViewModels;

public abstract partial class DownloaderViewModelBase : ViewModelBase
{
    public abstract ScriptDescriptor Script { get; }

    [ObservableProperty] private ObservableCollection<ScriptParameter> _parameters = [];
    [ObservableProperty] private ObservableCollection<LogEntry> _logEntries = [];
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = Strings.Get("STATUS_Ready");
    [ObservableProperty] private double _logMaxHeight = 400;

    private CancellationTokenSource? _cts;

    protected DownloaderViewModelBase()
    {
        foreach (var param in Script.Parameters)
        {
            Parameters.Add(param);
        }

        WeakReferenceMessenger.Default.Register<DownloaderViewModelBase, WindowSizeChangedMessage>(this, OnWindowSizeChanged);
    }

    private void OnWindowSizeChanged(object recipient, WindowSizeChangedMessage message)
    {
        LogMaxHeight = Math.Max(200, message.Value.Height * 0.5);
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogEntries.Clear();
    }

    [RelayCommand]
    private async Task CopyLogEntry(LogEntry entry)
    {
        var topLevel = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        var clipboard = topLevel?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(entry.FormattedLine);
        }
    }

    [RelayCommand]
    private async Task ExecuteScript()
    {
        if (IsRunning) return;

        IsRunning = true;
        StatusText = Strings.Get("STATUS_Running", Script.Name);
        _cts = new CancellationTokenSource();

        // 注册到 TaskRegistry，主程序退出时可检测
        var taskScope = new TaskScope(Script.Name, "B2C3D4E5-F6A7-8901-BCDE-DOWNLOADER001");
        // 将 TaskScope 的取消令牌链接到本地 CTS
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, taskScope.Token.CancellationTokenSource.Token);

        try
        {
            var paramValues = BuildParameterValues();
            await ExecuteCoreAsync(paramValues, linkedCts.Token);
            StatusText = Strings.Get("STATUS_Completed");
        }
        catch (OperationCanceledException)
        {
            StatusText = Strings.Get("STATUS_Cancelled");
        }
        catch (Exception ex)
        {
            AddLogEntry(new LogEntry { Message = Strings.Get("FMT_ExecuteFailed", ex.Message) });
            StatusText = Strings.Get("STATUS_Failed");
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
            taskScope.Dispose();
        }
    }

    [RelayCommand]
    private void CancelExecution()
    {
        _cts?.Cancel();
        StatusText = Strings.Get("STATUS_Cancelling");
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
            if (LogEntries.Count > 1100)
            {
                // 批量裁剪：一次移除 100 条，避免每条新日志都触发 O(N) 数组移位
                for (int i = 0; i < 100; i++)
                    LogEntries.RemoveAt(0);
            }
        });
    }
}
