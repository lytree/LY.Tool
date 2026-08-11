using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.TDLSharp.Models;
using LYBox.Plugin.TDLSharp.Resources;
using LYBox.Plugin.TDLSharp.Services;

namespace LYBox.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_BatchForwardChannel")]
[Menu("NAV_TDL_BatchForwardChannel", "TDL_BatchForwardChannel", ParentKey = "NAV_TDL", Order = 7)]
[ViewMap(typeof(Pages.BatchForwardToChannelPage))]
public partial class BatchForwardToChannelViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
    {
        Id = "batch-forward-channel",
        Name = Strings.Get("SCRIPT_BatchForwardChannel_Name"),
        Description = Strings.Get("SCRIPT_BatchForwardChannel_Desc"),
        Parameters =
        [
            ScriptParameter.MultiLineText("source", Strings.Get("PARAM_Source"), Strings.Get("PARAM_SourceDesc"), required: true),
            ScriptParameter.HistoryText("sourceId", Strings.Get("PARAM_SourceId"), Strings.Get("PARAM_SourceIdDesc"), required: false),
            ScriptParameter.HistoryText("target", Strings.Get("PARAM_Target"), Strings.Get("PARAM_TargetChannelDesc"), required: true),
            ScriptParameter.Switch("older", Strings.Get("PARAM_Older"), Strings.Get("PARAM_OlderDesc"), true),
            ScriptParameter.Number("limit", Strings.Get("PARAM_Limit"), Strings.Get("PARAM_LimitDesc"), 0),
            ScriptParameter.Switch("comments", Strings.Get("PARAM_Comments"), Strings.Get("PARAM_CommentsDesc"), true),
            ScriptParameter.Text("tags", Strings.Get("PARAM_Tags"), Strings.Get("PARAM_TagsDesc")),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        var source = paramValues.GetValueOrDefault("source", "");
        var sourceId = paramValues.GetValueOrDefault("sourceId");
        var target = paramValues.GetValueOrDefault("target", "");
        var older = bool.TryParse(paramValues.GetValueOrDefault("older", "true"), out var o) && o;
        var limit = int.TryParse(paramValues.GetValueOrDefault("limit", "0"), out var l) ? l : 0;
        var comments = bool.TryParse(paramValues.GetValueOrDefault("comments", "true"), out var c) && c;
        var tags = paramValues.GetValueOrDefault("tags");

        // 频道模式：不启用固定话题、不启用按源分类
        await tdlService.BatchForwardClassifiedAsync(
            source, sourceId, target,
            fixedTopicName: null,
            older, limit, comments,
            classifyBySource: false,
            tags, ct);
    }
}
