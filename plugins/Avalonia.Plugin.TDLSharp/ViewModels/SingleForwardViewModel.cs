using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_SingleForward")]
[Menu("NAV_TDL_SingleForward", "TDL_SingleForward", ParentKey = "NAV_TDL", Order = 4)]
[ViewMap(typeof(Pages.SingleForwardPage))]
public partial class SingleForwardViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
    {
        Id = "single-forward",
        Name = "单条深度转发",
        Description = "将单条消息深度转发到目标频道/群聊（自动收集同组媒体消息一起转发）",
        Parameters =
        [
            ScriptParameter.HistoryText("source", "源消息链接", "源频道/群聊中的具体消息链接", required: true),
            ScriptParameter.HistoryText("target", "目标链接", "目标频道/群聊链接或用户名", required: true),
            ScriptParameter.Switch("comments", "转发评论", "是否同时转发该消息的评论", true),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        await tdlService.SingleForwardAsync(
            paramValues.GetValueOrDefault("source", ""),
            paramValues.GetValueOrDefault("target", ""),
            bool.TryParse(paramValues.GetValueOrDefault("comments", "true"), out var comments) && comments,
            ct);
    }
}
