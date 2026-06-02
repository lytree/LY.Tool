using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_DeleteForwards")]
[Menu("NAV_TDL_DeleteForwards", "TDL_DeleteForwards", ParentKey = "NAV_TDL", Order = 6)]
[ViewMap(typeof(Pages.TdlScriptPage))]
public partial class DeleteForwardsViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => new()
    {
        Id = "delete-forwards",
        Name = "删除转发消息",
        Description = "删除频道/群聊中所有转发来源的消息，可指定从某条消息链接开始往前删除",
        Parameters =
        [
            ScriptParameter.Text("channel", "频道/群聊", "频道/群聊链接或用户名 (留空=收藏夹)", required: false),
            ScriptParameter.Text("fromLink", "起始消息链接", "指定消息链接，仅删除该消息之前的转发消息 (留空=从头开始)", required: false),
            ScriptParameter.Number("limit", "最大删除数量", "0=全部", 0),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        await tdlService.DeleteAllForwardMessagesAsync(
            paramValues.GetValueOrDefault("channel"),
            paramValues.GetValueOrDefault("fromLink"),
            int.TryParse(paramValues.GetValueOrDefault("limit", "0"), out var limit) ? limit : 0,
            ct);
    }
}