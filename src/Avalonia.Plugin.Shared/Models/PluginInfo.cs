namespace Avalonia.Plugin.Shared.Models;

public record PluginInfo
{
    public string PluginId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<string> Dependencies { get; init; } = [];
    public string InstallPath { get; init; } = string.Empty;
    public string AssemblyPath { get; init; } = string.Empty;
    public PluginState State { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime? InstallTime { get; init; }
    public bool IsBuiltIn { get; init; }
    public bool HasMetadata { get; init; }

    public PluginInfo WithState(PluginState state, string? errorMessage = null) =>
        this with { State = state, ErrorMessage = errorMessage };

    public PluginInfo WithInstallPath(string installPath) =>
        this with { InstallPath = installPath };

    public PluginInfo WithAssemblyPath(string assemblyPath) =>
        this with { AssemblyPath = assemblyPath };

    public PluginInfo WithInstallInfo(string installPath, string assemblyPath, PluginState state, DateTime? installTime) =>
        this with { InstallPath = installPath, AssemblyPath = assemblyPath, State = state, InstallTime = installTime };

    public PluginInfo WithMetadata(bool hasMetadata) =>
        this with { HasMetadata = hasMetadata };
}
