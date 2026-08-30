using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.TDLSharp.Models;
using LYBox.Plugin.TDLSharp.Resources;
using LYBox.Plugin.TDLSharp.Services;

namespace LYBox.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_DeepCopy")]
[Menu("NAV_TDL_DeepCopy", "TDL_DeepCopy", ParentKey = "NAV_TDL", Order = 9)]
[ViewMap(typeof(Pages.DeepCopyPage))]
public partial class DeepCopyViewModel : TdlViewModelBase
{
    protected override ScriptDescriptor CreateScript() => new()
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
        var bag = new ScriptParameterBag(paramValues);
        paramValues.TryGetValue("source", out var sourceRaw);
        var sourceRawTrimmed = sourceRaw?.Trim();
        var limit = bag.GetInt("limit");
        var comments = bag.GetBool("comments", true);
        var maxNonShallow = bag.GetInt("maxNonShallow", 5000);

        var sources = ParseSources(sourceRawTrimmed);
        if (sources.Count == 0) sources.Add("");

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

            await tdlService.DeleteShallowForwardMessagesAsync(chatId, maxNonShallow, ct);
        }
    }

    private static List<string> ParseSources(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        return raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
