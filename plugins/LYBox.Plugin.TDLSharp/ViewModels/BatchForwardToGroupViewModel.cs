using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.TDLSharp.Models;
using LYBox.Plugin.TDLSharp.Resources;
using LYBox.Plugin.TDLSharp.Services;

namespace LYBox.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_BatchForwardGroup")]
[Menu("NAV_TDL_BatchForwardGroup", "TDL_BatchForwardGroup", ParentKey = "NAV_TDL", Order = 8)]
[ViewMap(typeof(Pages.BatchForwardToGroupPage))]
public partial class BatchForwardToGroupViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
    {
        Id = "batch-forward-group",
        Name = Strings.Get("SCRIPT_BatchForwardGroup_Name"),
        Description = Strings.Get("SCRIPT_BatchForwardGroup_Desc"),
        Parameters =
        [
            ScriptParameter.MultiLineText("source", Strings.Get("PARAM_Source"), Strings.Get("PARAM_SourceDesc"), required: true),
            ScriptParameter.HistoryText("sourceId", Strings.Get("PARAM_SourceId"), Strings.Get("PARAM_SourceIdDesc"), required: false),
            ScriptParameter.HistoryText("target", Strings.Get("PARAM_Target"), Strings.Get("PARAM_TargetGroupDesc"), required: true),
            ScriptParameter.Text("fixedTopic", Strings.Get("PARAM_FixedTopic"), Strings.Get("PARAM_FixedTopicDesc")),
            ScriptParameter.Switch("older", Strings.Get("PARAM_Older"), Strings.Get("PARAM_OlderDesc"), true),
            ScriptParameter.Number("limit", Strings.Get("PARAM_Limit"), Strings.Get("PARAM_LimitDesc"), 0),
            ScriptParameter.Switch("comments", Strings.Get("PARAM_Comments"), Strings.Get("PARAM_CommentsDesc"), true),
            ScriptParameter.Switch("classify", Strings.Get("PARAM_ClassifyBySource"), Strings.Get("PARAM_ClassifyBySourceDesc"), false),
            ScriptParameter.Text("tags", Strings.Get("PARAM_Tags"), Strings.Get("PARAM_TagsDesc")),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        var source = paramValues.GetValueOrDefault("source", "");
        var sourceId = paramValues.GetValueOrDefault("sourceId");
        var target = paramValues.GetValueOrDefault("target", "");
        var fixedTopic = paramValues.GetValueOrDefault("fixedTopic");
        var older = bool.TryParse(paramValues.GetValueOrDefault("older", "true"), out var o) && o;
        var limit = int.TryParse(paramValues.GetValueOrDefault("limit", "0"), out var l) ? l : 0;
        var comments = bool.TryParse(paramValues.GetValueOrDefault("comments", "true"), out var c) && c;
        var classify = bool.TryParse(paramValues.GetValueOrDefault("classify", "false"), out var cls) && cls;
        var tags = paramValues.GetValueOrDefault("tags");

        // 群聊模式：可选固定话题（优先级高于 classify），可选按源分类
        await tdlService.BatchForwardClassifiedAsync(
            source, sourceId, target,
            fixedTopicName: string.IsNullOrWhiteSpace(fixedTopic) ? null : fixedTopic,
            older, limit, comments,
            classifyBySource: classify,
            tags, ct);
    }
}
