using System.CommandLine;
using LYBox.Plugin.Shared.CommandLine;
using Spectre.Console;

namespace LYBox.Plugin.Template;

public sealed class TemplateCliRegistrar : IPluginCommandRegistrar
{
    public string PluginId => "TEMPLATE-PLUGIN-0000-0000-000000000001";
    public string CommandName => "template";
    public string Description => "Template plugin commands.";

    public void RegisterCommands(PluginCommandRegistrationContext context)
    {
        var name = new Option<string>("--name")
        {
            Description = "Name included in the greeting.",
            Required = true
        };
        name.Aliases.Add("-n");

        var hello = new Command("hello", "Create a greeting.");
        hello.Options.Add(name);
        hello.SetAction(parseResult =>
        {
            context.Console.WriteLine($"Hello, {parseResult.GetRequiredValue(name)}!");
            return PluginCliExitCodes.Success;
        });
        context.Command.Subcommands.Add(hello);
    }
}
