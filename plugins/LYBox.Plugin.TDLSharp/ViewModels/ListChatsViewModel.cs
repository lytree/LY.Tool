using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.TDLSharp.Models;
using LYBox.Plugin.TDLSharp.Resources;
using LYBox.Plugin.TDLSharp.Services;

namespace LYBox.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_ListChats")]
[Menu("NAV_TDL_ListChats", "TDL_ListChats", ParentKey = "NAV_TDL", Order = 1)]
[ViewMap(typeof(Pages.ListChatsPage))]
public partial class ListChatsViewModel : TdlViewModelBase
{
    protected override ScriptDescriptor CreateScript() => new()
    {
        Id = "list-chats",
        Name = Strings.Get("SCRIPT_ListChats_Name"),
        Description = Strings.Get("SCRIPT_ListChats_Desc"),
        Parameters =
        [
            ScriptParameter.Text("output", Strings.Get("PARAM_Output"), Strings.Get("PARAM_OutputDesc")),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        paramValues.TryGetValue("output", out var output);
        await tdlService.ListChatsAsync(output, ct);
    }
}
