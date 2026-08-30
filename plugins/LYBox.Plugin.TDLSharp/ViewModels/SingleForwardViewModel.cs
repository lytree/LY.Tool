using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.TDLSharp.Models;
using LYBox.Plugin.TDLSharp.Resources;
using LYBox.Plugin.TDLSharp.Services;

namespace LYBox.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_SingleForward")]
[Menu("NAV_TDL_SingleForward", "TDL_SingleForward", ParentKey = "NAV_TDL", Order = 6)]
[ViewMap(typeof(Pages.SingleForwardPage))]
public partial class SingleForwardViewModel : TdlViewModelBase
{
    protected override ScriptDescriptor CreateScript() => new()
    {
        Id = "single-forward",
        Name = Strings.Get("SCRIPT_SingleForward_Name"),
        Description = Strings.Get("SCRIPT_SingleForward_Desc"),
        Parameters =
        [
            ScriptParameter.HistoryText("source", Strings.Get("PARAM_Source"), Strings.Get("PARAM_SingleSourceDesc"), required: true),
            ScriptParameter.HistoryText("target", Strings.Get("PARAM_Target"), Strings.Get("PARAM_SingleForwardTargetDesc"), required: true),
            ScriptParameter.Text("targetTopic", Strings.Get("PARAM_TargetTopic"), Strings.Get("PARAM_TargetTopicDesc")),
            ScriptParameter.Switch("comments", Strings.Get("PARAM_ForwardComments"), Strings.Get("PARAM_ForwardCommentsDesc"), true),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        var bag = new ScriptParameterBag(paramValues);
        var targetTopic = bag.GetString("targetTopic");
        await tdlService.SingleForwardAsync(
            bag.GetString("source"),
            bag.GetString("target"),
            forwardComments: bag.GetBool("comments", true),
            topicName: string.IsNullOrWhiteSpace(targetTopic) ? null : targetTopic,
            ct);
    }
}
