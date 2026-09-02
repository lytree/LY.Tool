namespace LYBox.Plugin.Shared.CommandLine;

/// <summary>Host initialization required by a CLI invocation.</summary>
public enum PluginCliExecutionProfile
{
    None,
    Desktop,
    CatalogOnly,
    SelectedPlugin,
    SelectedPluginData
}

/// <summary>Runtime services requested by a plugin CLI sidecar.</summary>
public enum PluginCliRuntimeProfile
{
    Selected,
    Data
}

/// <summary>Machine-stable process exit codes used by the LYBox CLI.</summary>
public static class PluginCliExitCodes
{
    public const int Success = 0;
    public const int Usage = 2;
    public const int InvalidConfiguration = 3;
    public const int NotFound = 4;
    public const int Conflict = 5;
    public const int Unsupported = 6;
    public const int Security = 7;
    public const int ValidationFailed = 8;
    public const int PluginFailed = 9;
    public const int PartialSuccess = 10;
    public const int HostFailure = 20;
    public const int Cancelled = 130;
}

/// <summary>Build-generated CLI index stored next to plugin.json.</summary>
public sealed class PluginCliIndex
{
    public int SchemaVersion { get; set; } = 1;
    public string PluginId { get; set; } = string.Empty;
    public string PluginVersion { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RuntimeProfile { get; set; } = "selected";
    public List<string> OutputModes { get; set; } = ["text"];
    public List<PluginCliCommandDefinition> Commands { get; set; } = [];

    public PluginCliRuntimeProfile GetRuntimeProfile() =>
        string.Equals(RuntimeProfile, "data", StringComparison.OrdinalIgnoreCase)
            ? PluginCliRuntimeProfile.Data
            : PluginCliRuntimeProfile.Selected;

    public bool SupportsOutput(string outputMode) =>
        OutputModes.Any(mode => string.Equals(mode, outputMode, StringComparison.OrdinalIgnoreCase));
}

public sealed class PluginCliCommandDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
