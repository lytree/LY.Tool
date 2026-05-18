using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_GroupMediaDownload")]
[Menu("群组媒体下载", "TDL_GroupMediaDownload", ParentKey = "TDL", Order = 4)]
[ViewMap(typeof(Pages.TdlScriptPage))]
public partial class GroupMediaDownloadViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
    {
        Id = "group-media-download",
        Name = "群组媒体下载",
        Description = "下载群组/频道中的媒体文件",
        Parameters =
        [
            ScriptParameter.Text("link", "消息链接", "Telegram消息链接 (多个用逗号分隔)", required: true),
            ScriptParameter.Path("output", "输出目录", "下载文件保存目录"),
            ScriptParameter.Switch("includeComments", "包含评论", "是否下载评论区媒体", true),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        await tdlService.GroupMediaDownloadAsync(
            paramValues.GetValueOrDefault("link", ""),
            paramValues.GetValueOrDefault("output"),
            bool.TryParse(paramValues.GetValueOrDefault("includeComments", "true"), out var inc) && inc,
            ct);
    }
}
