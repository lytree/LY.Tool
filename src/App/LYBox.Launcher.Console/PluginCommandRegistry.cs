using LYBox.Plugin.Shared.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace LYBox.Launcher.Console;

/// <summary>
/// 把 <see cref="IPluginCommandRegistrar"/> 服务集合中的注册器挂到根 <c>plugin</c> 子命令下。
/// <para>
/// 校验规则：
/// </para>
/// <list type="bullet">
/// <item>每个 <c>PluginId</c> 只能注册一次（防止插件重复装配）。</item>
/// <item>每个 <c>CommandName</c> 全局唯一（小写归一化，跨插件不允许同名）。</item>
/// <item>命令名仅允许字母 / 数字 / 连字符，避免 shell 转义陷阱。</item>
/// </list>
/// </summary>
internal static class PluginCommandRegistry
{
    /// <summary>注册插件命令，返回成功注册的数量。</summary>
    public static int RegisterCommands(
        System.CommandLine.Command pluginCommand,
        IServiceProvider services,
        IAnsiConsole console,
        IEnumerable<IGeneratedPluginCliModule> modules,
        string? targetPluginId = null)
    {
        ArgumentNullException.ThrowIfNull(pluginCommand);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(console);

        var commandNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pluginIds = new HashSet<string>(StringComparer.Ordinal);
        var registered = 0;

        foreach (var registrar in GetRegistrars(services, modules, targetPluginId))
        {
            if (!pluginIds.Add(registrar.PluginId))
            {
                throw new InvalidOperationException(
                    $"插件 '{registrar.PluginId}' 重复注册了 CLI 命令。每个插件只能注册一次 IPluginCommandRegistrar。");
            }

            var commandName = NormalizeCommandName(registrar.CommandName);
            if (string.Equals(commandName, "run", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("插件 CLI 命令名 'run' 为显式插件调用保留。");
            if (!commandNames.Add(commandName))
            {
                throw new InvalidOperationException(
                    $"插件 CLI 命令名 '{commandName}' 已被占用。请用更具体的 CommandName（例如 '{registrar.PluginId}.{commandName}'）。");
            }

            var command = new System.CommandLine.Command(commandName, registrar.Description);
            registrar.RegisterCommands(new PluginCommandRegistrationContext(command, services, console));
            pluginCommand.Subcommands.Add(command);
            registered++;
        }

        return registered;
    }

    public static int RegisterExplicitCommands(
        System.CommandLine.Command runCommand,
        IServiceProvider services,
        IAnsiConsole console,
        IEnumerable<IGeneratedPluginCliModule> modules,
        string targetPluginId)
    {
        ArgumentNullException.ThrowIfNull(runCommand);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPluginId);

        var registrars = GetRegistrars(services, modules, targetPluginId).ToArray();
        if (registrars.Length == 0)
            return 0;
        if (registrars.Length > 1)
        {
            throw new InvalidOperationException(
                $"插件 '{targetPluginId}' 重复注册了 CLI 命令。每个插件只能注册一次 IPluginCommandRegistrar。");
        }

        registrars[0].RegisterCommands(
            new PluginCommandRegistrationContext(runCommand, services, console));
        return 1;
    }

    public static int RegisterCommands(
        System.CommandLine.Command pluginCommand,
        IServiceProvider services,
        IAnsiConsole console) =>
        RegisterCommands(pluginCommand, services, console, []);

    private static IEnumerable<IPluginCommandRegistrar> GetRegistrars(
        IServiceProvider services,
        IEnumerable<IGeneratedPluginCliModule> modules,
        string? targetPluginId)
    {
        var generated = modules.SelectMany(module => module.CliRegistrars)
            .Select(descriptor => descriptor.CreateRegistrar(services));
        var registeredServices = services.GetServices<IPluginCommandRegistrar>();
        return generated.Concat(registeredServices)
            .DistinctBy(value => (value.GetType(), value.PluginId, value.CommandName))
            .Where(value => targetPluginId is null
                || string.Equals(value.PluginId, targetPluginId, StringComparison.Ordinal));
    }

    /// <summary>把命令名归一化为小写 trim，校验字符白名单。</summary>
    internal static string NormalizeCommandName(string commandName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);

        var normalized = commandName.Trim().ToLowerInvariant();
        if (normalized.Any(character => !char.IsLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException(
                "插件 CLI 命令名仅允许包含字母、数字和连字符。",
                nameof(commandName));
        }

        return normalized;
    }
}
