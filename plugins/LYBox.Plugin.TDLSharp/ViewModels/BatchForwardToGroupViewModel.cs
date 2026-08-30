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
    protected override ScriptDescriptor CreateScript() => new()
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
        var bag = new ScriptParameterBag(paramValues);
        var fixedTopic = bag.GetString("fixedTopic");
        var tags = bag.GetString("tags");
        await tdlService.BatchForwardClassifiedAsync(
            bag.GetString("source"),
            bag.GetString("sourceId"),
            bag.GetString("target"),
            fixedTopicName: string.IsNullOrWhiteSpace(fixedTopic) ? null : fixedTopic,
            older: bag.GetBool("older", true),
            limit: bag.GetInt("limit"),
            forwardComments: bag.GetBool("comments", true),
            classifyBySource: bag.GetBool("classify"),
            tags: string.IsNullOrEmpty(tags) ? null : tags,
            ct);
    }
}
