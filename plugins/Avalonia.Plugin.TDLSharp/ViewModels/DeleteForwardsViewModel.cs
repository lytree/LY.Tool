using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Resources;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_DeleteForwards")]
[Menu("NAV_TDL_DeleteForwards", "TDL_DeleteForwards", ParentKey = "NAV_TDL", Order = 9)]
[ViewMap(typeof(Pages.DeleteForwardsPage))]
public partial class DeleteForwardsViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
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
        await tdlService.DeleteAllForwardMessagesAsync(
            paramValues.GetValueOrDefault("channel"),
            paramValues.GetValueOrDefault("fromLink"),
            int.TryParse(paramValues.GetValueOrDefault("limit", "0"), out var limit) ? limit : 0,
            ct);
    }
}