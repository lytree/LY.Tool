using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_BatchForward")]
[Menu("批量深度转发", "TDL_BatchForward", ParentKey = "TDL", Order = 1)]
[ViewMap(typeof(Pages.TdlScriptPage))]
public partial class BatchForwardViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
    {
        Id = "batch-forward",
        Name = "批量深度转发",
        Description = "将源频道/群聊的消息批量深度转发到目标频道/群聊",
        Parameters =
        [
            ScriptParameter.Text("source", "源消息链接", "源频道/群聊消息链接", required: true),
            ScriptParameter.Text("sourceId", "源消息ID", "指定源消息ID (可选)", required: false),
            ScriptParameter.Text("target", "目标链接", "目标频道/群聊链接或用户名", required: true),
            ScriptParameter.Switch("older", "向旧消息方向", "true=向旧消息转发, false=向新消息转发", true),
            ScriptParameter.Number("limit", "最大转发数量", "0=全部", 0),
            ScriptParameter.Switch("comments", "转发评论", "是否转发评论", true),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        await tdlService.BatchForwardAsync(
            paramValues.GetValueOrDefault("source", ""),
            paramValues.GetValueOrDefault("sourceId"),
            paramValues.GetValueOrDefault("target", ""),
            bool.TryParse(paramValues.GetValueOrDefault("older", "true"), out var older) && older,
            int.TryParse(paramValues.GetValueOrDefault("limit", "0"), out var limit) ? limit : 0,
            bool.TryParse(paramValues.GetValueOrDefault("comments", "true"), out var comments) && comments,
            ct);
    }
}
