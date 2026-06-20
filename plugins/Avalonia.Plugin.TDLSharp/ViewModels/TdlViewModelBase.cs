using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Services;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Resources;
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
    [ObservableProperty] private string _statusText = Strings.Get("STATUS_Ready");
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

        // Check authentication before execution
        var clientManager = ServiceLocator.GetService<TdlClientManager>();
        if (clientManager == null) return;

        // 1. 检查 TDL 根目录是否设置
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

        // 2. 确保客户端已初始化并已回报认证状态
        //    AuthState 为 Unknown 时初始化客户端，等待 TDLib 回报首个认证状态
        await clientManager.EnsureReadyForAuthCheckAsync();

        // 3. 根据认证状态判断是否需要弹出登录界面
        //    NeedsLogin 涵盖所有 Wait* 状态及 Unknown/Closed/LoggingOut/Closing
        if (clientManager.NeedsLogin)
        {
            // 已知具体认证状态时直接弹出登录界面（无需二次确认）
            // 状态包括：WaitPhoneNumber / WaitCode / WaitPassword / WaitRegistration /
            //          WaitOtherDeviceConfirmation / WaitEmailAddress / WaitEmailCode /
            //          WaitPremiumPurchase / Unknown / Closed / LoggingOut / Closing
            await LoginDialogService.ShowLoginDialogAsync();
            return;
        }

        IsRunning = true;
        StatusText = Strings.Get("STATUS_Running", Script.Name);
        _cts = new CancellationTokenSource();

        var sw = Stopwatch.StartNew();
        var paramSnapshot = BuildParameterValues();

        // 注册到 TaskRegistry，主程序退出时可检测
        var taskScope = new TaskScope(Script.Name, "A1B2C3D4-E5F6-7890-ABCD-TDLSHARP00001");
        // 将 TaskScope 的取消令牌链接到本地 CTS
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, taskScope.Token.CancellationTokenSource.Token);

        // 执行开始时即创建历史记录
        var record = new ExecutionHistoryRecord
        {
            ScriptId = Script.Id,
            ScriptName = Script.Name,
            ParametersJson = JsonSerializer.Serialize(paramSnapshot, new JsonSerializerOptions { WriteIndented = false }),
            ParameterSummary = BuildParameterSummary(paramSnapshot),
            ExecutedAt = DateTime.Now,
            Duration = TimeSpan.Zero,
            Status = Strings.Get("STATUS_Executing"),
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
            await ExecuteCoreAsync(tdlService, paramSnapshot, linkedCts.Token);
            record.Status = Strings.Get("RESULT_Success");
            StatusText = Strings.Get("STATUS_Completed");
        }
        catch (OperationCanceledException)
        {
            record.Status = Strings.Get("STATUS_Cancelled");
            StatusText = Strings.Get("STATUS_Cancelled");
        }
        catch (Exception ex)
        {
            record.Status = Strings.Get("RESULT_Failed");
            record.ErrorMessage = ex.Message;
            AddLogEntry(new LogEntry { Message = Strings.Get("FMT_ExecuteFailed", ex.Message) });
            StatusText = Strings.Get("STATUS_Failed");
        }
        finally
        {
            sw.Stop();
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
            taskScope.Dispose();
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
        StatusText = Strings.Get("STATUS_Cancelling");
    }

    protected abstract Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct);

    protected virtual void OnExecutionFinished() { }

    protected DirectUiLogger CreateUiLogger()
    {
        return new DirectUiLogger(
            message => AddLogEntry(new LogEntry { Message = message }),
            entry => AddLogEntry(entry),
            UpdateProgressEntry);
    }

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
        catch (Exception ex) { Debug.WriteLine($"[TdlViewModel] 应用参数 JSON 失败: {ex.Message}"); }
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
        catch (Exception ex) { Debug.WriteLine($"[TdlViewModel] 加载执行历史失败: {ex.Message}"); }
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
            catch (Exception ex) { Debug.WriteLine($"[TdlViewModel] 后台加载执行历史失败: {ex.Message}"); }
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
        catch (Exception ex) { Debug.WriteLine($"[TdlViewModel] 保存执行历史记录失败: {ex.Message}"); }
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
        catch (Exception ex) { Debug.WriteLine($"[TdlViewModel] 更新执行历史记录失败: {ex.Message}"); }
    }
}
