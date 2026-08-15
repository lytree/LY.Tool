namespace LYBox.Plugin.Shared.Attributes;

/// <summary>
/// 标注一个方法为可被前端 JavaScript 调用的 RPC 命令。
/// 由 LYBox.Plugin.Generators 的 RpcCommandGenerator 扫描并生成绑定注册代码。
/// </summary>
/// <remarks>
/// 用法：
/// <code>
/// public partial class CounterService
/// {
///     [RpcCommand]
///     public Task&lt;int&gt; AddAsync(int a, int b) => Task.FromResult(a + b);
/// }
/// </code>
/// 命令名为 "<paramref name="Name"/>"（缺省为方法名）。前端通过
/// <c>window.go.&lt;Namespace&gt;.&lt;Class&gt;.&lt;Name&gt;(...args)</c> 调用，
/// 返回 Promise。方法可为实例或静态；实例方法所在类须有公共无参构造函数
/// （生成代码会创建单例实例）。
/// 方法参数与返回值必须可被 System.Text.Json 序列化。
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RpcCommandAttribute : Attribute
{
    /// <summary>命令名。缺省为方法名。</summary>
    public string? Name { get; }

    public RpcCommandAttribute() { }

    public RpcCommandAttribute(string name) => Name = name;
}
