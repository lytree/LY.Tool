using System.Collections.ObjectModel;
using Avalonia.Input.Platform;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

public abstract partial class TdlViewModelBase : ViewModelBase
{
    public abstract ScriptDescriptor Script { get; }

    [ObservableProperty] private ObservableCollection<ScriptParameter> _parameters = [];
    [ObservableProperty] private ObservableCollection<LogEntry> _logEntries = [];
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = "就绪";

    private CancellationTokenSource? _cts;

    protected TdlViewModelBase()
    {
        foreach (var param in Script.Parameters)
        {
            Parameters.Add(param);
        }
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
        StatusText = $"正在执行: {Script.Name}...";
        _cts = new CancellationTokenSource();

        try
        {
            var paramValues = BuildParameterValues();
            var tdlService = CreateTdlService();

            await ExecuteCoreAsync(tdlService, paramValues, _cts.Token);
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
            OnExecutionFinished();
        }
    }

    [RelayCommand]
    private void CancelExecution()
    {
        _cts?.Cancel();
        StatusText = "正在取消...";
    }

    protected abstract Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct);

    protected virtual void OnExecutionFinished() { }

    protected DirectUiLogger CreateUiLogger()
    {
        return new DirectUiLogger(message => AddLogEntry(new LogEntry { Message = message }));
    }

    protected TdlService CreateTdlService()
    {
        var clientManager = ServiceLocator.GetService<TdlClientManager>();
        var logger = CreateUiLogger();
        return new TdlService(clientManager, logger);
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
