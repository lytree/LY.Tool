using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Resources;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_Download")]
[Menu("NAV_TDL_Download", "TDL_Download", ParentKey = "NAV_TDL", Order = 4)]
[ViewMap(typeof(Pages.DownloadPage))]
public partial class DownloadViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
    {
        Id = "download",
        Name = Strings.Get("SCRIPT_Download_Name"),
        Description = Strings.Get("SCRIPT_Download_Desc"),
        Parameters =
        [
            ScriptParameter.MultiLineText("links", Strings.Get("PARAM_Links"), Strings.Get("PARAM_LinksDesc"), required: true),
            ScriptParameter.Path("output", Strings.Get("PARAM_Output"), Strings.Get("PARAM_OutputDesc")),
            ScriptParameter.Text("include", Strings.Get("PARAM_Include"), Strings.Get("PARAM_IncludeDesc")),
            ScriptParameter.Text("exclude", Strings.Get("PARAM_Exclude"), Strings.Get("PARAM_ExcludeDesc")),
            ScriptParameter.Switch("desc", Strings.Get("PARAM_Desc"), Strings.Get("PARAM_DescDesc"), false),
            ScriptParameter.Switch("group", Strings.Get("PARAM_Group"), Strings.Get("PARAM_GroupDesc"), true),
            ScriptParameter.Switch("skipSame", Strings.Get("PARAM_SkipSame"), Strings.Get("PARAM_SkipSameDesc"), true),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        await tdlService.DownloadFilesAsync(
            paramValues.GetValueOrDefault("links", ""),
            paramValues.GetValueOrDefault("output", ""),
            paramValues.GetValueOrDefault("include"),
            paramValues.GetValueOrDefault("exclude"),
            bool.TryParse(paramValues.GetValueOrDefault("desc", "false"), out var desc) && desc,
            bool.TryParse(paramValues.GetValueOrDefault("group", "true"), out var group) && group,
            bool.TryParse(paramValues.GetValueOrDefault("skipSame", "true"), out var skipSame) && skipSame,
            ct);
    }
}
