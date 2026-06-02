using System.Collections.ObjectModel;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_DeepCopy")]
[Menu("NAV_TDL_DeepCopy", "TDL_DeepCopy", ParentKey = "NAV_TDL", Order = 3)]
[ViewMap(typeof(Pages.DeepCopyPage))]
public partial class DeepCopyViewModel : TdlViewModelBase
{
    [ObservableProperty] private ObservableCollection<DeepCopyHistoryRecord> _historyRecords = [];
    [ObservableProperty] private DeepCopyHistoryRecord? _selectedHistoryRecord;

    public override ScriptDescriptor Script => new()
    {
        Id = "forward",
        Name = "深度Copy转发",
        Description = "将频道中的浅转发消息转换为深度Copy（从原始来源重新发送副本，然后删除旧浅转发）\n支持同时输入多个频道，每行一个",
        Parameters =
        [
            ScriptParameter.MultiLineText("source", "源频道", "每行输入一个频道/群聊链接或用户名\n留空=收藏夹", required: false),
            ScriptParameter.Number("limit", "最大处理数量", "0=全部", 0),
            ScriptParameter.Switch("comments", "处理评论", "是否同时处理评论中的浅转发", true),
        ]
    };

    public DeepCopyViewModel()
    {
        LoadHistory();
    }

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        var sourceRaw = paramValues.GetValueOrDefault("source")?.Trim();
        var limit = int.TryParse(paramValues.GetValueOrDefault("limit", "0"), out var l) ? l : 0;
        var comments = bool.TryParse(paramValues.GetValueOrDefault("comments", "true"), out var c) && c;

        var sources = ParseSources(sourceRaw);

        if (sources.Count == 0)
            sources.Add("");

        for (int i = 0; i < sources.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var source = sources[i];
            var channelLabel = string.IsNullOrWhiteSpace(source) ? "收藏夹" : source;

            if (sources.Count > 1)
                AddLogEntry(new LogEntry { Message = $"━━━ 处理频道 [{i + 1}/{sources.Count}]: {channelLabel} ━━━" });

            var record = new DeepCopyHistoryRecord
            {
                SourceChannel = channelLabel,
                ExecutedAt = DateTime.Now,
                Status = "执行中"
            };

            try
            {
                await tdlService.DeepCopyAsync(source, limit, comments, ct);

                var chatId = await tdlService.ResolveChatIdAsync(source);
                if (chatId == 0)
                {
                    var currentUser = await tdlService.GetCurrentUserAsync();
                    chatId = currentUser.Id;
                }

                await tdlService.DeleteShallowForwardMessagesAsync(chatId, ct);

                record.Status = "成功";
            }
            catch (OperationCanceledException)
            {
                record.Status = "部分完成";
                throw;
            }
            catch (Exception ex)
            {
                record.Status = "失败";
                record.ErrorMessage = ex.Message;
            }

            record.ExecutedAt = DateTime.Now;
            await SaveHistoryRecordAsync(record);

            Dispatcher.UIThread.Post(() =>
            {
                HistoryRecords.Insert(0, record);
                if (HistoryRecords.Count > 100)
                    HistoryRecords.RemoveAt(HistoryRecords.Count - 1);
            });
        }
    }

    [RelayCommand]
    private void ClearHistory()
    {
        HistoryRecords.Clear();
        _ = ClearHistoryDbAsync();
    }

    [RelayCommand]
    private void DeleteHistoryRecord(DeepCopyHistoryRecord record)
    {
        HistoryRecords.Remove(record);
        _ = DeleteHistoryRecordDbAsync(record);
    }

    [RelayCommand]
    private void ApplyHistorySource(DeepCopyHistoryRecord record)
    {
        var sourceParam = Parameters.FirstOrDefault(p => p.Key == "source");
        if (sourceParam != null)
        {
            var existingLines = (sourceParam.DefaultValue ?? "")
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (!existingLines.Contains(record.SourceChannel) && record.SourceChannel != "收藏夹")
            {
                existingLines.Add(record.SourceChannel);
                sourceParam.DefaultValue = string.Join(Environment.NewLine, existingLines);
            }
            else if (record.SourceChannel == "收藏夹")
            {
                sourceParam.DefaultValue = string.Join(Environment.NewLine, existingLines);
            }
        }
    }

    private static List<string> ParseSources(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        return raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void LoadHistory()
    {
        Task.Run(async () =>
        {
            try
            {
                using var db = CreateHistoryDbContext();
                await db.Database.EnsureCreatedAsync();
                var records = await db.HistoryRecords
                    .OrderByDescending(r => r.ExecutedAt)
                    .Take(100)
                    .ToListAsync();

                Dispatcher.UIThread.Post(() =>
                {
                    foreach (var r in records)
                        HistoryRecords.Add(r);
                });
            }
            catch { }
        });
    }

    private async Task SaveHistoryRecordAsync(DeepCopyHistoryRecord record)
    {
        try
        {
            using var db = CreateHistoryDbContext();
            await db.Database.EnsureCreatedAsync();
            db.HistoryRecords.Add(record);
            await db.SaveChangesAsync();
        }
        catch { }
    }

    private async Task ClearHistoryDbAsync()
    {
        try
        {
            using var db = CreateHistoryDbContext();
            await db.Database.EnsureCreatedAsync();
            db.HistoryRecords.RemoveRange(db.HistoryRecords);
            await db.SaveChangesAsync();
        }
        catch { }
    }

    private async Task DeleteHistoryRecordDbAsync(DeepCopyHistoryRecord record)
    {
        try
        {
            using var db = CreateHistoryDbContext();
            await db.Database.EnsureCreatedAsync();
            var entity = await db.HistoryRecords.FindAsync(record.Id);
            if (entity != null)
            {
                db.HistoryRecords.Remove(entity);
                await db.SaveChangesAsync();
            }
        }
        catch { }
    }

    private static DeepCopyHistoryDbContext CreateHistoryDbContext()
    {
        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AvaloniaTemplate", "TDLSharp");
        Directory.CreateDirectory(dataDir);
        return new DeepCopyHistoryDbContext(Path.Combine(dataDir, "deepcopy-history.db"));
    }
}
