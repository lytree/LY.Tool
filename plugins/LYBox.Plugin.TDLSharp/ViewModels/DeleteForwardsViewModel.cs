using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.TDLSharp.Models;
using LYBox.Plugin.TDLSharp.Resources;
using LYBox.Plugin.TDLSharp.Services;

namespace LYBox.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_DeleteForwards")]
[Menu("NAV_TDL_DeleteForwards", "TDL_DeleteForwards", ParentKey = "NAV_TDL", Order = 10)]
[ViewMap(typeof(Pages.DeleteForwardsPage))]
public partial class DeleteForwardsViewModel : TdlViewModelBase
{
    protected override ScriptDescriptor CreateScript() => new()
    {
        Id = "delete-forwards",
        Name = Strings.Get("SCRIPT_DeleteForwards_Name"),
        Description = Strings.Get("SCRIPT_DeleteForwards_Desc"),
        Parameters =
        [
            ScriptParameter.HistoryText("channel", Strings.Get("PARAM_Channel"), Strings.Get("PARAM_ChannelDesc"), required: false),
            ScriptParameter.HistoryText("fromLink", Strings.Get("PARAM_FromLink"), Strings.Get("PARAM_FromLinkDesc"), required: false),
            ScriptParameter.Number("limit", Strings.Get("PARAM_MaxDelete"), Strings.Get("PARAM_MaxDeleteDesc"), 0),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        var bag = new ScriptParameterBag(paramValues);
        paramValues.TryGetValue("channel", out var channel);
        paramValues.TryGetValue("fromLink", out var fromLink);
        await tdlService.DeleteAllForwardMessagesAsync(channel, fromLink, bag.GetInt("limit"), ct);
    }
}
