using System.Text.Json;
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
    public CliFailureException(
        int exitCode,
        string code,
        string message,
        object? details = null,
        Exception? innerException = null)
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
        if (Format == CliOutputFormat.Json)
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

    public void WriteDiagnostic(string message) =>
        _errorConsole.MarkupLine($"[yellow]{SpectreMarkup.Escape(message)}[/]");

    private void WriteJson(object value)
    {
        _standardOutput.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
        _standardOutput.Flush();
    }
}
