using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_ClearMessage")]
[Menu("清理消息", "TDL_ClearMessage", ParentKey = "TDL", Order = 2)]
[ViewMap(typeof(Pages.TdlScriptPage))]
public partial class ClearMessageViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
    {
        Id = "clear-message",
        Name = "清理消息",
        Description = "清理频道中包含指定内容的消息",
        Parameters =
        [
            ScriptParameter.Text("channel", "频道/群聊", "频道/群聊链接或用户名 (留空=收藏夹)", required: false),
            ScriptParameter.Text("contains", "匹配文本", "匹配消息中包含的文本内容", "This channel can't be displayed"),
            ScriptParameter.Switch("silent", "静默删除", "静默删除，不询问确认", false),
            ScriptParameter.Number("limit", "最大处理数量", "0=全部", 0),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        await tdlService.ClearMessagesAsync(
            paramValues.GetValueOrDefault("channel"),
            paramValues.GetValueOrDefault("contains", ""),
            bool.TryParse(paramValues.GetValueOrDefault("silent", "false"), out var silent) && silent,
            int.TryParse(paramValues.GetValueOrDefault("limit", "0"), out var limit) ? limit : 0,
            ct);
    }
}
