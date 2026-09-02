using System.Text.Json;
using LYBox.Plugin.Shared.CommandLine;
using Spectre.Console;
using SpectreMarkup = Spectre.Console.Markup;

namespace LYBox.Launcher.Console;

internal enum CliOutputFormat
{
    Text,
    Json
}

internal sealed class CliFailureException : Exception
{
    public CliFailureException(int exitCode, string code, string message, object? details = null, Exception? innerException = null)
        : base(message, innerException)
    {
        ExitCode = exitCode;
        Code = code;
        Details = details;
    }

    public int ExitCode { get; }
    public string Code { get; }
    public object? Details { get; }
}

internal sealed class CliOutput
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IAnsiConsole _outputConsole;
    private readonly IAnsiConsole _errorConsole;
    private readonly TextWriter _standardOutput;

    public CliOutput(
        CliOutputFormat format,
        IAnsiConsole outputConsole,
        IAnsiConsole errorConsole,
        TextWriter standardOutput)
    {
        Format = format;
        _outputConsole = outputConsole;
        _errorConsole = errorConsole;
        _standardOutput = standardOutput;
    }

    public CliOutputFormat Format { get; }
    public IAnsiConsole Console => _outputConsole;

    public void WriteSuccess(string command, object? data = null)
    {
        if (Format != CliOutputFormat.Json) return;
        WriteJson(new { schemaVersion = 1, ok = true, command, data });
    }

    public void WriteFailure(string command, CliFailureException failure)
    {
        if (Format == CliOutputFormat.Json)
        {
            WriteJson(new
            {
                schemaVersion = 1,
                ok = false,
                command,
                error = new
                {
                    code = failure.Code,
                    exitCode = failure.ExitCode,
                    message = failure.Message,
                    details = failure.Details
                }
            });
            return;
        }

        _errorConsole.MarkupLine($"[red]{SpectreMarkup.Escape(failure.Message)}[/]");
    }

    public void WriteDiagnostic(string message)
    {
        _errorConsole.MarkupLine($"[yellow]{SpectreMarkup.Escape(message)}[/]");
    }

    private void WriteJson(object value)
    {
        _standardOutput.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
        _standardOutput.Flush();
    }
}

internal static class CliArguments
{
    public static (CliOutputFormat Format, string[] Arguments) ExtractOutput(string[] args)
    {
        var format = CliOutputFormat.Text;
        var remaining = new List<string>(args.Length);

        for (var index = 0; index < args.Length; index++)
        {
            var value = args[index];
            string? requested = null;
            if (string.Equals(value, "--output", StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= args.Length)
                    throw Usage("--output requires 'text' or 'json'.");
                requested = args[index];
            }
            else if (value.StartsWith("--output=", StringComparison.OrdinalIgnoreCase))
            {
                requested = value[9..];
            }
            else
            {
                remaining.Add(value);
            }

            if (requested is null) continue;
            format = requested.ToLowerInvariant() switch
            {
                "text" => CliOutputFormat.Text,
                "json" => CliOutputFormat.Json,
                _ => throw Usage($"Unsupported output mode '{requested}'. Use 'text' or 'json'.")
            };
        }

        return (format, remaining.ToArray());
    }

    private static CliFailureException Usage(string message) =>
        new(PluginCliExitCodes.Usage, "invalid_arguments", message);
}

internal static class CliInvocationClassifier
{
    public static PluginCliExecutionProfile Classify(string[] args)
    {
        if (args.Length == 0) return PluginCliExecutionProfile.Desktop;

        var first = args[0];
        if (string.Equals(first, "gui", StringComparison.OrdinalIgnoreCase)
            || string.Equals(first, "desktop", StringComparison.OrdinalIgnoreCase))
        {
            return PluginCliExecutionProfile.Desktop;
        }

        if (string.Equals(first, "plugins", StringComparison.OrdinalIgnoreCase))
            return PluginCliExecutionProfile.CatalogOnly;

        if (!string.Equals(first, "plugin", StringComparison.OrdinalIgnoreCase))
            return PluginCliExecutionProfile.None;

        if (args.Length == 1 || args.Skip(1).Any(IsHelpOption))
            return PluginCliExecutionProfile.CatalogOnly;

        return PluginCliExecutionProfile.SelectedPlugin;
    }

    public static bool IsHelpOption(string value) => value is "--help" or "-h" or "-?";
}
