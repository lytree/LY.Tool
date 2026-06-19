using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Resources;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_DeepCopy")]
[Menu("NAV_TDL_DeepCopy", "TDL_DeepCopy", ParentKey = "NAV_TDL", Order = 8)]
[ViewMap(typeof(Pages.DeepCopyPage))]
public partial class DeepCopyViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
    {
        Id = "forward",
        Name = Strings.Get("SCRIPT_DeepCopy_Name"),
        Description = Strings.Get("SCRIPT_DeepCopy_Desc"),
        Parameters =
        [
            ScriptParameter.HistoryText("source", Strings.Get("PARAM_SourceChannel"), Strings.Get("PARAM_SourceChannelDesc"), required: false),
            ScriptParameter.Number("limit", Strings.Get("PARAM_Limit"), Strings.Get("PARAM_LimitDesc"), 0),
            ScriptParameter.Switch("comments", Strings.Get("PARAM_ProcessComments"), Strings.Get("PARAM_ProcessCommentsDesc"), true),
            ScriptParameter.Number("maxNonShallow", Strings.Get("PARAM_MaxNonShallow"), Strings.Get("PARAM_MaxNonShallowDesc"), 5000),
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
            var channelLabel = string.IsNullOrWhiteSpace(source) ? Strings.Get("WORD_Favorites") : source;

            if (sources.Count > 1)
                AddLogEntry(new LogEntry { Message = Strings.Get("FMT_ProcessingChannel", i + 1, sources.Count, channelLabel) });

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
