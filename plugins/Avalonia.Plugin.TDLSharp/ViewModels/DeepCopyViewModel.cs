using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_DeepCopy")]
[Menu("深度Copy转发", "TDL_DeepCopy", ParentKey = "TDL", Order = 3)]
[ViewMap(typeof(Pages.TdlScriptPage))]
public partial class DeepCopyViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
    {
        Id = "forward",
        Name = "深度Copy转发",
        Description = "将频道中的浅转发消息转换为深度Copy（从原始来源重新发送副本，然后删除旧浅转发）",
        Parameters =
        [
            ScriptParameter.Text("source", "源频道", "源频道/群聊链接或用户名 (留空=收藏夹)", required: false),
            ScriptParameter.Number("limit", "最大处理数量", "0=全部", 0),
            ScriptParameter.Switch("comments", "处理评论", "是否同时处理评论中的浅转发", true),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        var source = paramValues.GetValueOrDefault("source");
        var limit = int.TryParse(paramValues.GetValueOrDefault("limit", "0"), out var l) ? l : 0;
        var comments = bool.TryParse(paramValues.GetValueOrDefault("comments", "true"), out var c) && c;

        await tdlService.DeepCopyAsync(source, limit, comments, ct);

        var chatId = await tdlService.ResolveChatIdAsync(source);
        if (chatId == 0)
        {
            var currentUser = await tdlService.GetCurrentUserAsync();
            chatId = currentUser.Id;
        }

        await tdlService.DeleteShallowForwardMessagesAsync(chatId, ct);
    }
}
