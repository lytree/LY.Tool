using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.TDLSharp.Models;
using LYBox.Plugin.TDLSharp.Resources;
using LYBox.Plugin.TDLSharp.Services;

namespace LYBox.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_ClearMessage")]
[Menu("NAV_TDL_ClearMessage", "TDL_ClearMessage", ParentKey = "NAV_TDL", Order = 11)]
[ViewMap(typeof(Pages.ClearMessagePage))]
public partial class ClearMessageViewModel : TdlViewModelBase
{
    protected override ScriptDescriptor CreateScript() => new()
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
        var bag = new ScriptParameterBag(paramValues);
        paramValues.TryGetValue("channel", out var channel);
        await tdlService.ClearMessagesAsync(
            channel,
            bag.GetString("contains"),
            silent: bag.GetBool("silent"),
            limit: bag.GetInt("limit"),
            ct);
    }
}
