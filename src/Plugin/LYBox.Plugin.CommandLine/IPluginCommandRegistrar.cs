using System.CommandLine;
using Spectre.Console;

namespace LYBox.Plugin.Shared.CommandLine;

/// <summary>
/// Implemented by plugins that contribute commands to LYBox.Launcher.Console.
/// The implementation must be registered as an <see cref="IPluginCommandRegistrar"/>
/// during plugin service configuration.
/// </summary>
public interface IPluginCommandRegistrar
{
    /// <summary>Gets the unique plugin identifier used for duplicate detection.</summary>
    string PluginId { get; }

    /// <summary>Gets the command name exposed below <c>lybox plugin</c>.</summary>
    string CommandName { get; }

    /// <summary>Gets the description displayed in command help.</summary>
    string Description { get; }

    /// <summary>Registers plugin-specific arguments, options, and handlers.</summary>
    void RegisterCommands(PluginCommandRegistrationContext context);
}

/// <summary>Provides host services used while a plugin registers its command tree.</summary>
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

    /// <summary>Gets the plugin root command.</summary>
    public Command Command { get; }

    /// <summary>Gets the host service provider containing plugin services.</summary>
    public IServiceProvider Services { get; }

    /// <summary>Gets the host-configured Spectre console.</summary>
    public IAnsiConsole Console { get; }
}
