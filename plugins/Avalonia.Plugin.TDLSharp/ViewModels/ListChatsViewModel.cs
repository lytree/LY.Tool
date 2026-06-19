using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Resources;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_ListChats")]
[Menu("NAV_TDL_ListChats", "TDL_ListChats", ParentKey = "NAV_TDL", Order = 1)]
[ViewMap(typeof(Pages.ListChatsPage))]
public partial class ListChatsViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
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
        await tdlService.ListChatsAsync(
            paramValues.GetValueOrDefault("output"),
            ct);
    }
}
