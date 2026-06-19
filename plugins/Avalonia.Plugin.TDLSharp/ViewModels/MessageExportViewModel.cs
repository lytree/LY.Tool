using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Resources;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_MessageExport")]
[Menu("NAV_TDL_MessageExport", "TDL_MessageExport", ParentKey = "NAV_TDL", Order = 3)]
[ViewMap(typeof(Pages.MessageExportPage))]
public partial class MessageExportViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
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
        await tdlService.ExportMessagesAsync(
            paramValues.GetValueOrDefault("channel", ""),
            paramValues.GetValueOrDefault("output"),
            bool.TryParse(paramValues.GetValueOrDefault("comments", "false"), out var comments) && comments,
            int.TryParse(paramValues.GetValueOrDefault("limit", "0"), out var limit) ? limit : 0,
            ct);
    }
}
