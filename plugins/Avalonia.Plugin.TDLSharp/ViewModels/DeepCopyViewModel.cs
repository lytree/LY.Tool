using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_DeepCopy")]
[Menu("NAV_TDL_DeepCopy", "TDL_DeepCopy", ParentKey = "NAV_TDL", Order = 3)]
[ViewMap(typeof(Pages.DeepCopyPage))]
public partial class DeepCopyViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
    {
        Id = "forward",
        Name = "深度Copy转发",
        Description = "将频道中的浅转发消息转换为深度Copy（从原始来源重新发送副本，然后删除旧浅转发）\n支持同时输入多个频道，每行一个",
        Parameters =
        [
            ScriptParameter.HistoryText("source", "源频道", "每行输入一个频道/群聊链接或用户名\n留空=收藏夹", required: false),
            ScriptParameter.Number("limit", "最大处理数量", "0=全部", 0),
            ScriptParameter.Switch("comments", "处理评论", "是否同时处理评论中的浅转发", true),
            ScriptParameter.Number("maxNonShallow", "非浅转发阈值", "连续N条非浅转发消息后停止扫描", 5000),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        var sourceRaw = paramValues.GetValueOrDefault("source")?.Trim();
        var limit = int.TryParse(paramValues.GetValueOrDefault("limit", "0"), out var l) ? l : 0;
        var comments = bool.TryParse(paramValues.GetValueOrDefault("comments", "true"), out var c) && c;
        var maxNonShallow = int.TryParse(paramValues.GetValueOrDefault("maxNonShallow", "5000"), out var m) ? m : 5000;

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

            await tdlService.DeepCopyAsync(source, limit, comments, maxNonShallow, ct);

            var chatId = await tdlService.ResolveChatIdAsync(source);
            if (chatId == 0)
            {
                var currentUser = await tdlService.GetCurrentUserAsync();
                chatId = currentUser.Id;
            }

            await tdlService.DeleteShallowForwardMessagesAsync(chatId, ct);
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
}
