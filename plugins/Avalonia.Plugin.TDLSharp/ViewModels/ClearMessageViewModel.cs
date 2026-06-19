using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Resources;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_ClearMessage")]
[Menu("NAV_TDL_ClearMessage", "TDL_ClearMessage", ParentKey = "NAV_TDL", Order = 10)]
[ViewMap(typeof(Pages.ClearMessagePage))]
public partial class ClearMessageViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
    {
        Id = "clear-message",
        Name = Strings.Get("SCRIPT_ClearMessage_Name"),
        Description = Strings.Get("SCRIPT_ClearMessage_Desc"),
        Parameters =
        [
            ScriptParameter.HistoryText("channel", Strings.Get("PARAM_Channel"), Strings.Get("PARAM_ChannelDesc"), required: false),
            ScriptParameter.HistoryText("contains", Strings.Get("PARAM_Contains"), Strings.Get("PARAM_ContainsDesc"), "This channel can't be displayed"),
            ScriptParameter.Switch("silent", Strings.Get("PARAM_Silent"), Strings.Get("PARAM_SilentDesc"), false),
            ScriptParameter.Number("limit", Strings.Get("PARAM_Limit"), Strings.Get("PARAM_LimitDesc"), 0),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        await tdlService.ClearMessagesAsync(
            paramValues.GetValueOrDefault("channel"),
            paramValues.GetValueOrDefault("contains", ""),
            bool.TryParse(paramValues.GetValueOrDefault("silent", "false"), out var silent) && silent,
            int.TryParse(paramValues.GetValueOrDefault("limit", "0"), out var limit) ? limit : 0,
            ct);
    }
}
