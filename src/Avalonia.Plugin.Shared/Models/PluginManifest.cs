namespace Avalonia.Plugin.Shared.Models;

public class PluginManifest
{
    public string? PluginId { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string? Assembly { get; set; }
    public List<string>? Dependencies { get; set; }
    public string? State { get; set; }
    public DateTime? InstallTime { get; set; }
    public bool IsBuiltIn { get; set; }
}
