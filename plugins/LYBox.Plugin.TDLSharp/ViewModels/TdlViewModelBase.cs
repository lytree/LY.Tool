using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;
using LYBox.Plugin.TDLSharp.Models;
using LYBox.Plugin.TDLSharp.Resources;
using LYBox.Plugin.TDLSharp.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Ursa.Controls;

namespace LYBox.Plugin.TDLSharp.ViewModels;

public abstract partial class TdlViewModelBase : ViewModelBase
{
    private ScriptDescriptor? _script;

    [ObservableProperty] private ObservableCollection<ScriptParameter> _parameters = [];
    [ObservableProperty] private ObservableCollection<LogEntry> _logEntries = [];
    [ObservableProperty] private ObservableCollection<ExecutionHistoryRecord> _executionHistoryRecords = [];
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = Strings.Get("STATUS_Ready");
    [ObservableProperty] private double _logMaxHeight = 400;

    private CancellationTokenSource? _cts;

    /// <summary>
    /// 脚本元数据（包含参数定义）。首次访问时构造并缓存。
    /// 注意：构造时基类会复制 <see cref="CreateScript"/> 返回的 <c>Parameters</c> 到
    /// <see cref="Parameters"/> 集合；之后用户对 <see cref="Parameters"/> 中参数值的修改
    /// 不会反映到 <see cref="Script"/>，也不会影响执行。
    /// </summary>
    public ScriptDescriptor Script => _script ??= CreateScript();

    /// <summary>由子类实现：构造脚本元数据。基类只会调用一次。</summary>
    protected abstract ScriptDescriptor CreateScript();

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
            Title = Strings.Get("FMT_ExecutionHistoryTitle", Script.Name),
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
            var text = entry.IsProgress
                ? $"{entry.FileName} - {entry.StatusText} ({entry.ProgressValue:F1}%)"
                : entry.FormattedLine;
            await clipboard.SetTextAsync(text);
        }
    }

    [RelayCommand]
    private async Task ExecuteScript()
    {
        if (IsRunning) return;

        var clientManager = ServiceLocator.GetService<TdlClientManager>();
        if (clientManager == null) return;

        if (!clientManager.HasTdlRoot)
        {
            var result = await OverlayMessageBox.ShowAsync(
                Strings.Get("LOGIN_TdlRootNotSetWarning"),
                Strings.Get("LOGIN_NotInitializedTitle"),
                button: MessageBoxButton.YesNo,
                icon: MessageBoxIcon.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await LoginDialogService.ShowLoginDialogAsync();
            }
            return;
        }

        await clientManager.EnsureReadyForAuthCheckAsync();

        if (clientManager.NeedsLogin)
        {
            await LoginDialogService.ShowLoginDialogAsync();
            return;
        }

        IsRunning = true;
        StatusText = string.Format(Strings.Get("STATUS_Running"), Script.Name);
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        var paramSnapshot = BuildParameterValues();
        var historyRecord = new ExecutionHistoryRecord
        {
            ScriptId = Script.Id,
            ScriptName = Script.Name,
            ParametersJson = JsonSerializer.Serialize(paramSnapshot),
            ParameterSummary = BuildParameterSummary(paramSnapshot),
            ExecutedAt = DateTime.Now,
            Status = "执行中",
        };
        var historyStart = DateTime.UtcNow;
        await SaveExecutionHistoryRecordAsync(historyRecord);

        TdlService? tdlService = null;
        try
        {
            tdlService = CreateTdlService();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            await ExecuteCoreAsync(tdlService, paramSnapshot, linkedCts.Token);
            historyRecord.Status = "成功";
            StatusText = Strings.Get("STATUS_Completed");
        }
        catch (OperationCanceledException)
        {
            historyRecord.Status = "已取消";
            StatusText = Strings.Get("STATUS_Cancelled");
        }
        catch (Exception ex)
        {
            historyRecord.Status = "失败";
            historyRecord.ErrorMessage = ex.Message;
            StatusText = $"{Strings.Get("STATUS_Failed")}: {ex.Message}";
            Debug.WriteLine($"[TdlViewModel] 脚本执行异常: {ex}");
        }
        finally
        {
            historyRecord.Duration = DateTime.UtcNow - historyStart;
            await UpdateExecutionHistoryRecordAsync(historyRecord);
            IsRunning = false;
        }
    }

    [RelayCommand]
    private void CancelExecution()
    {
        _cts?.Cancel();
        StatusText = Strings.Get("STATUS_Cancelling");
    }

    /// <summary>由子类实现：执行具体脚本逻辑。</summary>
    protected abstract Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct);

    protected DirectUiLogger CreateUiLogger() => new(
        message => AddLogEntry(new LogEntry { Message = message }),
        entry => AddLogEntry(entry),
        UpdateProgressEntry);

    protected static void UpdateProgressEntry(LogEntry entry, double progressValue, string status, bool completed, bool failed)
    {
        Dispatcher.UIThread.Post(() =>
        {
            entry.ProgressValue = progressValue;
            entry.StatusText = status;
            entry.IsCompleted = completed;
            entry.IsFailed = failed;
        });
    }

    protected TdlService CreateTdlService()
    {
        var clientManager = ServiceLocator.GetService<TdlClientManager>();
        var logger = CreateUiLogger();
        return new TdlService(clientManager, logger);
    }

    /// <summary>
    /// 直接添加一条已构造好的日志条目（用于进度条更新等需要外部控制 LogEntry 实例的场景）。
    /// </summary>
    public void AddLogEntryExternally(LogEntry entry) => AddLogEntry(entry);

    protected void AddLogEntry(LogEntry entry)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogEntries.Add(entry);
            const int MaxLogEntries = 1000;
            const int TrimBatch = 100;
            if (LogEntries.Count > MaxLogEntries + TrimBatch)
            {
                var toRemove = LogEntries.Count - MaxLogEntries;
                for (int i = 0; i < toRemove; i++)
                    LogEntries.RemoveAt(0);
            }
        });
    }

    private Dictionary<string, string> BuildParameterValues()
    {
        var values = new Dictionary<string, string>();
        foreach (var param in Parameters)
        {
            values[param.Key] = param.DefaultValue ?? string.Empty;
        }
        return values;
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
        catch (Exception ex) { Debug.WriteLine($"[TdlViewModel] 应用参数 JSON 失败: {ex.Message}"); }
    }

    private Task LoadExecutionHistoryAsync() => LoadExecutionHistoryCoreAsync(setCollection: true);

    private void LoadExecutionHistory() => _ = LoadExecutionHistoryCoreAsync(setCollection: false);

    async Task LoadExecutionHistoryCoreAsync(bool setCollection)
    {
        try
        {
            using var db = ExecutionHistoryDbContext.CreateForScript(Script.Id);
            await db.EnsureSchemaInitializedAsync();
            var records = await db.ExecutionRecords
                .Where(r => r.ScriptId == Script.Id)
                .OrderByDescending(r => r.ExecutedAt)
                .Take(200)
                .ToListAsync();

            void Apply()
            {
                ExecutionHistoryRecords.Clear();
                foreach (var r in records)
                    ExecutionHistoryRecords.Add(r);
            }

            if (setCollection)
            {
                Apply();
            }
            else
            {
                Dispatcher.UIThread.Post(Apply);
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[TdlViewModel] 加载执行历史失败: {ex.Message}"); }
    }

    private static string BuildParameterSummary(Dictionary<string, string> values)
    {
        var parts = new List<string>();
        foreach (var kvp in values)
        {
            if (string.IsNullOrWhiteSpace(kvp.Value)) continue;
            var shortVal = kvp.Value.Length > 40 ? kvp.Value[..37] + "..." : kvp.Value;
            parts.Add($"{kvp.Key}={shortVal}");
        }
        return string.Join("; ", parts);
    }

    private async Task SaveExecutionHistoryRecordAsync(ExecutionHistoryRecord record)
    {
        try
        {
            using var db = ExecutionHistoryDbContext.CreateForScript(Script.Id);
            await db.EnsureSchemaInitializedAsync();
            db.ExecutionRecords.Add(record);
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { Debug.WriteLine($"[TdlViewModel] 保存执行历史记录失败: {ex.Message}"); }
    }

    private async Task UpdateExecutionHistoryRecordAsync(ExecutionHistoryRecord record)
    {
        try
        {
            using var db = ExecutionHistoryDbContext.CreateForScript(Script.Id);
            await db.EnsureSchemaInitializedAsync();
            db.ExecutionRecords.Update(record);
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { Debug.WriteLine($"[TdlViewModel] 更新执行历史记录失败: {ex.Message}"); }
    }
}
