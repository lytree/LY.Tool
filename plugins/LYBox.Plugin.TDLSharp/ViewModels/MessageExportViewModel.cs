using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.TDLSharp.Models;
using LYBox.Plugin.TDLSharp.Resources;
using LYBox.Plugin.TDLSharp.Services;

namespace LYBox.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_MessageExport")]
[Menu("NAV_TDL_MessageExport", "TDL_MessageExport", ParentKey = "NAV_TDL", Order = 3)]
[ViewMap(typeof(Pages.MessageExportPage))]
public partial class MessageExportViewModel : TdlViewModelBase
{
    protected override ScriptDescriptor CreateScript() => new()
    {
        Id = "message-export",
        Name = Strings.Get("SCRIPT_MessageExport_Name"),
        Description = Strings.Get("SCRIPT_MessageExport_Desc"),
        Parameters =
        [
            ScriptParameter.HistoryText("channel", Strings.Get("PARAM_Channel"), Strings.Get("PARAM_ChannelDesc"), required: true),
            ScriptParameter.HistoryText("output", Strings.Get("PARAM_Output"), Strings.Get("PARAM_OutputDesc"), required: false),
            ScriptParameter.Switch("comments", Strings.Get("PARAM_ExportComments"), Strings.Get("PARAM_ExportCommentsDesc"), false),
            ScriptParameter.Number("limit", Strings.Get("PARAM_MaxExport"), Strings.Get("PARAM_MaxExportDesc"), 0),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        var bag = new ScriptParameterBag(paramValues);
        paramValues.TryGetValue("output", out var output);
        await tdlService.ExportMessagesAsync(
            bag.GetString("channel"),
            output,
            exportComments: bag.GetBool("comments"),
            limit: bag.GetInt("limit"),
            ct);
    }
}
