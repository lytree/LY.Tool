using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Resources;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_BatchForward")]
[Menu("NAV_TDL_BatchForward", "TDL_BatchForward", ParentKey = "NAV_TDL", Order = 7)]
[ViewMap(typeof(Pages.BatchForwardPage))]
public partial class BatchForwardViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
    {
        Id = "batch-forward",
        Name = Strings.Get("SCRIPT_BatchForward_Name"),
        Description = Strings.Get("SCRIPT_BatchForward_Desc"),
        Parameters =
        [
            ScriptParameter.HistoryText("source", Strings.Get("PARAM_Source"), Strings.Get("PARAM_SourceDesc"), required: true),
            ScriptParameter.HistoryText("sourceId", Strings.Get("PARAM_SourceId"), Strings.Get("PARAM_SourceIdDesc"), required: false),
            ScriptParameter.HistoryText("target", Strings.Get("PARAM_Target"), Strings.Get("PARAM_TargetDesc"), required: true),
            ScriptParameter.Switch("older", Strings.Get("PARAM_Older"), Strings.Get("PARAM_OlderDesc"), true),
            ScriptParameter.Number("limit", Strings.Get("PARAM_Limit"), Strings.Get("PARAM_LimitDesc"), 0),
            ScriptParameter.Switch("comments", Strings.Get("PARAM_Comments"), Strings.Get("PARAM_CommentsDesc"), true),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        await tdlService.BatchForwardAsync(
            paramValues.GetValueOrDefault("source", ""),
            paramValues.GetValueOrDefault("sourceId"),
            paramValues.GetValueOrDefault("target", ""),
            bool.TryParse(paramValues.GetValueOrDefault("older", "true"), out var older) && older,
            int.TryParse(paramValues.GetValueOrDefault("limit", "0"), out var limit) ? limit : 0,
            bool.TryParse(paramValues.GetValueOrDefault("comments", "true"), out var comments) && comments,
            ct);
    }
}
