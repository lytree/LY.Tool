using System.CommandLine;
using Spectre.Console;

namespace LYBox.Plugin.Shared.CommandLine;

/// <summary>
/// 由插件实现以向控制台启动器（<c>LYBox.Launcher.Console</c>）注册自定义 CLI 子命令。
/// 实现必须作为 <see cref="IPluginCommandRegistrar"/> 通过插件的
/// <c>InitializeAsync(IServiceCollection)</c> 注册为服务。
/// </summary>
/// <remarks>
/// <para>
/// 命令名（<see cref="CommandName"/>）必须唯一、仅包含字母 / 数字 / 连字符，
/// 启动器在 <c>plugin</c> 子命令下注册同名命令。
/// </para>
/// <para>
/// 注册的子命令拥有与宿主其余命令一致的解析、help、错误处理体验。
/// 实现负责用 <see cref="PluginCommandRegistrationContext.Command"/> 挂载具体子命令与选项。
/// </para>
/// </remarks>
public interface IPluginCommandRegistrar
{
    /// <summary>插件唯一标识，用于检测重复注册。</summary>
    string PluginId { get; }

    /// <summary>命令短名（如 <c>sample</c>），将作为 <c>lybox plugin sample</c> 暴露。</summary>
    string CommandName { get; }

    /// <summary>命令描述，展示在 help 输出中。</summary>
    string Description { get; }

    /// <summary>把插件子命令挂到 <see cref="PluginCommandRegistrationContext.Command"/> 上。</summary>
    void RegisterCommands(PluginCommandRegistrationContext context);
}

/// <summary>
/// 插件命令注册上下文，暴露给 <see cref="IPluginCommandRegistrar.RegisterCommands"/>。
/// </summary>
public sealed class PluginCommandRegistrationContext
{
    public PluginCommandRegistrationContext(
        Command command,
        IServiceProvider services,
        IAnsiConsole console)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Console = console ?? throw new ArgumentNullException(nameof(console));
    }

    /// <summary>插件专属根 <see cref="System.CommandLine.Command"/>；实现应把子命令挂到它上面。</summary>
    public Command Command { get; }

    /// <summary>宿主构建的 <see cref="IServiceProvider"/>；实现可通过 DI 拿到自身服务。</summary>
    public IServiceProvider Services { get; }

    /// <summary>Spectre.Console 富文本输出端；与宿主的 <see cref="IAnsiConsole"/> 共享设置（颜色 / 重定向）。</summary>
    public IAnsiConsole Console { get; }
}
