namespace LYBox.Plugin.Shared.Attributes;

/// <summary>
/// 标注一个方法为可被前端 JavaScript 调用的 RPC 命令。
/// 由 LYBox.Plugin.Generators 的 RpcCommandGenerator 扫描并生成绑定注册代码。
/// </summary>
/// <remarks>
/// 用法：
/// <code>
/// public sealed record AddRequest(int Left, int Right);
///
/// public partial class CounterService
/// {
///     [RpcCommand]
///     public Task&lt;int&gt; AddAsync(AddRequest request, CancellationToken cancellationToken)
///         => Task.FromResult(request.Left + request.Right);
/// }
/// </code>
/// 命令名缺省为方法名（亦可用 <paramref name="Name"/> 显式指定）。前端通过
/// <c>window.__lybox.rpc('&lt;Name&gt;', ...args)</c> 调用，返回 Promise。
/// 方法可为实例或静态；实例方法所在类须有公共无参构造函数
/// （生成代码会创建单例实例）。
/// 新代码应使用零个或一个业务 payload 参数，可额外声明 CancellationToken。
/// 多个位置参数继续受支持，但只作为旧协议兼容模式。参数与返回值必须可被 System.Text.Json 序列化。
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RpcCommandAttribute : Attribute
{
    /// <summary>命令名。缺省为方法名。</summary>
    public string? Name { get; }

    public RpcCommandAttribute() { }

    public RpcCommandAttribute(string name) => Name = name;
}
