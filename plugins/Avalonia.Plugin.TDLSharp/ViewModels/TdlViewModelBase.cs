using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Services;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Ursa.Controls;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

public abstract partial class TdlViewModelBase : ViewModelBase
{
    public abstract ScriptDescriptor Script { get; }

    [ObservableProperty] private ObservableCollection<ScriptParameter> _parameters = [];
    [ObservableProperty] private ObservableCollection<LogEntry> _logEntries = [];
    [ObservableProperty] private ObservableCollection<ExecutionHistoryRecord> _executionHistoryRecords = [];
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = "就绪";
    [ObservableProperty] private double _logMaxHeight = 400;

    private CancellationTokenSource? _cts;

    protected TdlViewModelBase()
    {
        foreach (var param in Script.Parameters)
        {
            Parameters.Add(param);
        }

        WeakReferenceMessenger.Default.Register<TdlViewModelBase, WindowSizeChangedMessage>(this, OnWindowSizeChanged);
        LoadExecutionHistory();
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
    private async Task ShowExecutionHistory()
    {
        await LoadExecutionHistoryAsync();
        var dialogVm = new ExecutionHistoryDialogViewModel(Script.Id, ExecutionHistoryRecords, ApplyParametersFromJson);
        var options = new OverlayDialogOptions
        {
            Title = $"执行历史 - {Script.Name}",
            CanResize = false,
            CanLightDismiss = true,
            IsCloseButtonVisible = true,
            HorizontalAnchor = HorizontalPosition.Center,
            VerticalAnchor = VerticalPosition.Center,
        };
        await OverlayDialog.ShowCustomAsync<Controls.ExecutionHistoryDialog, ExecutionHistoryDialogViewModel, bool>(dialogVm, options: options);
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

        var sw = Stopwatch.StartNew();
        var paramSnapshot = BuildParameterValues();

        // 执行开始时即创建历史记录
        var record = new ExecutionHistoryRecord
        {
            ScriptId = Script.Id,
            ScriptName = Script.Name,
            ParametersJson = JsonSerializer.Serialize(paramSnapshot, new JsonSerializerOptions { WriteIndented = false }),
            ParameterSummary = BuildParameterSummary(paramSnapshot),
            ExecutedAt = DateTime.Now,
            Duration = TimeSpan.Zero,
            Status = "执行中",
            ErrorMessage = null
        };
        await SaveExecutionHistoryRecordAsync(record);
        Dispatcher.UIThread.Post(() =>
        {
            ExecutionHistoryRecords.Insert(0, record);
            if (ExecutionHistoryRecords.Count > 200)
                ExecutionHistoryRecords.RemoveAt(ExecutionHistoryRecords.Count - 1);
        });

        try
        {
            var tdlService = CreateTdlService();
            await ExecuteCoreAsync(tdlService, paramSnapshot, _cts.Token);
            record.Status = "成功";
            StatusText = "执行完成";
        }
        catch (OperationCanceledException)
        {
            record.Status = "已取消";
            StatusText = "已取消";
        }
        catch (Exception ex)
        {
            record.Status = "失败";
            record.ErrorMessage = ex.Message;
            AddLogEntry(new LogEntry { Message = $"执行失败: {ex.Message}" });
            StatusText = "执行失败";
        }
        finally
        {
            sw.Stop();
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
            OnExecutionFinished();

            // 更新历史记录的最终状态
            record.Duration = sw.Elapsed;
            await UpdateExecutionHistoryRecordAsync(record);
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

    private void ApplyParametersFromJson(string parametersJson)
    {
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(parametersJson) ?? new();
            foreach (var param in Parameters)
            {
                if (values.TryGetValue(param.Key, out var val))
                    param.DefaultValue = val;
            }
        }
        catch { }
    }

    private async Task LoadExecutionHistoryAsync()
    {
        try
        {
            using var db = ExecutionHistoryDbContext.CreateForScript(Script.Id);
            await db.Database.EnsureCreatedAsync();
            var records = await db.ExecutionRecords
                .Where(r => r.ScriptId == Script.Id)
                .OrderByDescending(r => r.ExecutedAt)
                .Take(200)
                .ToListAsync();

            ExecutionHistoryRecords.Clear();
            foreach (var r in records)
                ExecutionHistoryRecords.Add(r);
        }
        catch { }
    }

    private string BuildParameterSummary(Dictionary<string, string> values)
    {
        var parts = new List<string>();
        foreach (var param in Parameters)
        {
            if (values.TryGetValue(param.Key, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var shortVal = val.Length > 40 ? val[..37] + "..." : val;
                parts.Add($"{param.DisplayName}={shortVal}");
            }
        }
        return string.Join("; ", parts);
    }

    private void LoadExecutionHistory()
    {
        Task.Run(async () =>
        {
            try
            {
                using var db = ExecutionHistoryDbContext.CreateForScript(Script.Id);
                await db.Database.EnsureCreatedAsync();
                var records = await db.ExecutionRecords
                    .Where(r => r.ScriptId == Script.Id)
                    .OrderByDescending(r => r.ExecutedAt)
                    .Take(200)
                    .ToListAsync();

                Dispatcher.UIThread.Post(() =>
                {
                    foreach (var r in records)
                        ExecutionHistoryRecords.Add(r);
                });
            }
            catch { }
        });
    }

    private async Task SaveExecutionHistoryRecordAsync(ExecutionHistoryRecord record)
    {
        try
        {
            using var db = ExecutionHistoryDbContext.CreateForScript(Script.Id);
            await db.Database.EnsureCreatedAsync();
            db.ExecutionRecords.Add(record);
            await db.SaveChangesAsync();
        }
        catch { }
    }

    private async Task UpdateExecutionHistoryRecordAsync(ExecutionHistoryRecord record)
    {
        try
        {
            using var db = ExecutionHistoryDbContext.CreateForScript(Script.Id);
            await db.Database.EnsureCreatedAsync();
            db.ExecutionRecords.Update(record);
            await db.SaveChangesAsync();
        }
        catch { }
    }
}
