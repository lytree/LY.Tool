using System.Collections.ObjectModel;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

public partial class ExecutionHistoryDialogViewModel : ObservableObject, IDialogContext
{
    private readonly Action<string>? _applyParametersCallback;
    private readonly string _scriptId;

    [ObservableProperty] private ExecutionHistoryRecord? _selectedRecord;

    public ObservableCollection<ExecutionHistoryRecord> Records { get; }

    public ExecutionHistoryDialogViewModel(
        string scriptId,
        ObservableCollection<ExecutionHistoryRecord> records,
        Action<string>? applyParametersCallback = null)
    {
        _scriptId = scriptId;
        Records = records;
        _applyParametersCallback = applyParametersCallback;
    }

    public void Close()
    {
        RequestClose?.Invoke(this, null);
    }

    public event EventHandler<object?>? RequestClose;

    [RelayCommand]
    private async Task DeleteRecord(ExecutionHistoryRecord? record)
    {
        if (record == null) return;

        using var db = ExecutionHistoryDbContext.CreateForScript(_scriptId);
        var existing = await db.ExecutionRecords.FindAsync(record.Id);
        if (existing != null)
        {
            db.ExecutionRecords.Remove(existing);
            await db.SaveChangesAsync();
        }

        Records.Remove(record);
    }

    [RelayCommand]
    private async Task ClearAll()
    {
        if (Records.Count == 0) return;

        using var db = ExecutionHistoryDbContext.CreateForScript(_scriptId);
        await db.ExecutionRecords
            .Where(r => r.ScriptId == _scriptId)
            .ExecuteDeleteAsync();

        Records.Clear();
    }

    [RelayCommand]
    private void ApplyParameters(ExecutionHistoryRecord? record)
    {
        if (record == null || _applyParametersCallback == null) return;
        _applyParametersCallback(record.ParametersJson);
        Close();
    }

    [RelayCommand]
    private void CloseDialog()
    {
        Close();
    }
}
