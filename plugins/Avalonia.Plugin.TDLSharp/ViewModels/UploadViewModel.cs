using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Resources;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_Upload")]
[Menu("NAV_TDL_Upload", "TDL_Upload", ParentKey = "NAV_TDL", Order = 5)]
[ViewMap(typeof(Pages.UploadPage))]
public partial class UploadViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
    {
        Id = "upload",
        Name = Strings.Get("SCRIPT_Upload_Name"),
        Description = Strings.Get("SCRIPT_Upload_Desc"),
        Parameters =
        [
            ScriptParameter.MultiLineText("paths", Strings.Get("PARAM_Paths"), Strings.Get("PARAM_PathsDesc"), required: true),
            ScriptParameter.HistoryText("chat", Strings.Get("PARAM_Chat"), Strings.Get("PARAM_ChatDesc")),
            ScriptParameter.Switch("photo", Strings.Get("PARAM_Photo"), Strings.Get("PARAM_PhotoDesc"), false),
            ScriptParameter.Switch("rm", Strings.Get("PARAM_Rm"), Strings.Get("PARAM_RmDesc"), false),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        await tdlService.UploadFilesAsync(
            paramValues.GetValueOrDefault("paths", ""),
            paramValues.GetValueOrDefault("chat"),
            bool.TryParse(paramValues.GetValueOrDefault("photo", "false"), out var photo) && photo,
            bool.TryParse(paramValues.GetValueOrDefault("rm", "false"), out var rm) && rm,
            ct);
    }
}
