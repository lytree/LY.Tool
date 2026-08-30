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
    protected override ScriptDescriptor CreateScript() => new()
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
        var bag = new ScriptParameterBag(paramValues);
        var tags = bag.GetString("tags");
        await tdlService.BatchForwardClassifiedAsync(
            bag.GetString("source"),
            bag.GetString("sourceId"),
            bag.GetString("target"),
            fixedTopicName: null,
            older: bag.GetBool("older", true),
            limit: bag.GetInt("limit"),
            forwardComments: bag.GetBool("comments", true),
            classifyBySource: false,
            tags: string.IsNullOrEmpty(tags) ? null : tags,
            ct);
    }
}
