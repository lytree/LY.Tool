using LYBox.Plugin.Shared.Attributes;

namespace LYBox.Plugin.WebTemplate.Rpc;

/// <summary>
/// 演示 [RpcCommand] 绑定。前端通过 window.go.LYBox.Plugin.WebTemplate.Rpc.GreetCommands.* 调用。
/// 类必须 partial（源生成器生成 IRpcBindingSource 实现），实例方法需公共无参构造函数。
/// </summary>
public partial class GreetCommands
{
    [RpcCommand]
    public Task<string> GreetAsync(GreetRequest request, CancellationToken cancellationToken)
        => Task.FromResult($"Hello, {request.Name}! 这是来自 C# 的问候。");

    [RpcCommand]
    public Task<int> AddAsync(AddRequest request, CancellationToken cancellationToken)
        => Task.FromResult(request.Left + request.Right);

    [RpcCommand]
    public Task<object> GetPluginInfoAsync()
        => Task.FromResult<object>(new
        {
            id = "8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d",
            name = "Web Template",
            version = "1.0.0",
            serverTime = DateTime.Now.ToString("o")
        });
}

public sealed record GreetRequest(string Name);

public sealed record AddRequest(int Left, int Right);
