using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_MessageExport")]
[Menu("消息导出", "TDL_MessageExport", ParentKey = "TDL", Order = 5)]
[ViewMap(typeof(Pages.TdlScriptPage))]
public partial class MessageExportViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
    {
        Id = "message-export",
        Name = "消息导出",
        Description = "导出频道消息为JSON (支持分组和评论)",
        Parameters =
        [
            ScriptParameter.Text("channel", "频道/群聊", "频道/群聊链接或用户名", required: true),
            ScriptParameter.Text("output", "输出路径", "输出文件路径 (留空=自动)", required: false),
            ScriptParameter.Switch("comments", "导出评论", "是否导出评论", false),
            ScriptParameter.Number("limit", "最大导出数量", "0=全部", 0),
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
