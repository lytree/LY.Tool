using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.TDLSharp.Models;
using LYBox.Plugin.TDLSharp.Resources;
using LYBox.Plugin.TDLSharp.Services;

namespace LYBox.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_Download")]
[Menu("NAV_TDL_Download", "TDL_Download", ParentKey = "NAV_TDL", Order = 4)]
[ViewMap(typeof(Pages.DownloadPage))]
public partial class DownloadViewModel : TdlViewModelBase
{
    protected override ScriptDescriptor CreateScript() => new()
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
            ScriptParameter.Switch("downloadComments", Strings.Get("PARAM_DownloadComments"), Strings.Get("PARAM_DownloadCommentsDesc"), false),
            ScriptParameter.Switch("sequential", Strings.Get("PARAM_Sequential"), Strings.Get("PARAM_SequentialDesc"), false),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        var bag = new ScriptParameterBag(paramValues);
        paramValues.TryGetValue("include", out var include);
        paramValues.TryGetValue("exclude", out var exclude);
        await tdlService.DownloadFilesAsync(
            bag.GetString("links"),
            bag.GetString("output"),
            include,
            exclude,
            desc: bag.GetBool("desc"),
            group: bag.GetBool("group", true),
            skipSame: bag.GetBool("skipSame", true),
            downloadComments: bag.GetBool("downloadComments"),
            sequential: bag.GetBool("sequential"),
            ct);
    }
}
