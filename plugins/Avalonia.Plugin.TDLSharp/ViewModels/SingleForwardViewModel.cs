using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Resources;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_SingleForward")]
[Menu("NAV_TDL_SingleForward", "TDL_SingleForward", ParentKey = "NAV_TDL", Order = 6)]
[ViewMap(typeof(Pages.SingleForwardPage))]
public partial class SingleForwardViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
    {
        Id = "single-forward",
        Name = Strings.Get("SCRIPT_SingleForward_Name"),
        Description = Strings.Get("SCRIPT_SingleForward_Desc"),
        Parameters =
        [
            ScriptParameter.HistoryText("source", Strings.Get("PARAM_Source"), Strings.Get("PARAM_SingleSourceDesc"), required: true),
            ScriptParameter.HistoryText("target", Strings.Get("PARAM_Target"), Strings.Get("PARAM_TargetDesc"), required: true),
            ScriptParameter.Switch("comments", Strings.Get("PARAM_ForwardComments"), Strings.Get("PARAM_ForwardCommentsDesc"), true),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        await tdlService.SingleForwardAsync(
            paramValues.GetValueOrDefault("source", ""),
            paramValues.GetValueOrDefault("target", ""),
            bool.TryParse(paramValues.GetValueOrDefault("comments", "true"), out var comments) && comments,
            ct);
    }
}
