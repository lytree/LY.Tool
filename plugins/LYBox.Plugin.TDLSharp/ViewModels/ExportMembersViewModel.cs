using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.TDLSharp.Models;
using LYBox.Plugin.TDLSharp.Resources;
using LYBox.Plugin.TDLSharp.Services;

namespace LYBox.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_ExportMembers")]
[Menu("NAV_TDL_ExportMembers", "TDL_ExportMembers", ParentKey = "NAV_TDL", Order = 2)]
[ViewMap(typeof(Pages.ExportMembersPage))]
public partial class ExportMembersViewModel : TdlViewModelBase
{
    protected override ScriptDescriptor CreateScript() => new()
    {
        Id = "export-members",
        Name = Strings.Get("SCRIPT_ExportMembers_Name"),
        Description = Strings.Get("SCRIPT_ExportMembers_Desc"),
        Parameters =
        [
            ScriptParameter.HistoryText("chat", Strings.Get("PARAM_Chat"), Strings.Get("PARAM_ChatDesc"), required: true),
            ScriptParameter.Text("output", Strings.Get("PARAM_Output"), Strings.Get("PARAM_OutputDesc")),
            ScriptParameter.Switch("raw", Strings.Get("PARAM_Raw"), Strings.Get("PARAM_RawDesc"), false),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        var bag = new ScriptParameterBag(paramValues);
        paramValues.TryGetValue("output", out var output);
        await tdlService.ExportMembersAsync(
            bag.GetString("chat"),
            output,
            raw: bag.GetBool("raw"),
            ct);
    }
}
