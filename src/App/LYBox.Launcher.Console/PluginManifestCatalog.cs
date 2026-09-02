using System.Text.Json;
using LYBox.Layout.Core.Services;
using LYBox.Plugin.Shared.CommandLine;
using LYBox.Plugin.Shared.Models;

namespace LYBox.Launcher.Console;

internal sealed record PluginCatalogEntry(
    PluginInfo Info,
    string ManifestPath,
    PluginCliIndex? CliIndex,
    string? CliIndexError);

internal sealed record PluginCliSelection(
    PluginCatalogEntry Target,
    IReadOnlyList<PluginCatalogEntry> LoadOrder,
    PluginCliExecutionProfile Profile);

internal sealed class PluginCatalogSnapshot
{
    private readonly IReadOnlyDictionary<string, PluginCatalogEntry> _plugins;

    public PluginCatalogSnapshot(
        IReadOnlyDictionary<string, PluginCatalogEntry> plugins,
        IReadOnlyList<string> diagnostics)
    {
        _plugins = plugins;
        Diagnostics = diagnostics;
    }

    public IReadOnlyCollection<PluginCatalogEntry> Plugins => _plugins.Values.ToArray();
    public IReadOnlyList<string> Diagnostics { get; }

    public PluginCatalogEntry ResolveExact(string pluginId)
    {
        if (_plugins.TryGetValue(pluginId, out var entry)) return entry;
        throw new CliFailureException(
            PluginCliExitCodes.NotFound,
            "plugin_not_found",
            $"Plugin '{pluginId}' was not found.",
            new { pluginId });
    }

    public PluginCatalogEntry ResolveAlias(string alias)
    {
        var matches = _plugins.Values
            .Where(entry => entry.CliIndex is not null
                && string.Equals(entry.CliIndex.Alias, alias, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length == 0)
        {
            throw new CliFailureException(
                PluginCliExitCodes.NotFound,
                "plugin_alias_not_found",
                $"Plugin alias '{alias}' was not found in plugin.cli.json.",
                new { alias });
        }

        if (matches.Length > 1)
        {
            throw new CliFailureException(
                PluginCliExitCodes.Conflict,
                "plugin_alias_conflict",
                $"Plugin alias '{alias}' is declared by more than one plugin.",
                new { alias, pluginIds = matches.Select(match => match.Info.PluginId).ToArray() });
        }

        return matches[0];
    }

    public IReadOnlyList<PluginCatalogEntry> ResolveDependencyOrder(PluginCatalogEntry target)
    {
        var ordered = new List<PluginCatalogEntry>();
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(PluginCatalogEntry entry)
        {
            if (visited.Contains(entry.Info.PluginId)) return;
            if (!visiting.Add(entry.Info.PluginId))
            {
                throw new CliFailureException(
                    PluginCliExitCodes.InvalidConfiguration,
                    "dependency_cycle",
                    $"Plugin dependency cycle includes '{entry.Info.PluginId}'.",
                    new { pluginId = entry.Info.PluginId });
            }

            foreach (var dependencyId in entry.Info.Dependencies.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!_plugins.TryGetValue(dependencyId, out var dependency))
                {
                    throw new CliFailureException(
                        PluginCliExitCodes.Unsupported,
                        "dependency_missing",
                        $"Plugin '{entry.Info.PluginId}' requires missing dependency '{dependencyId}'.",
                        new { pluginId = entry.Info.PluginId, dependencyId });
                }
                Visit(dependency);
            }

            visiting.Remove(entry.Info.PluginId);
            visited.Add(entry.Info.PluginId);
            ordered.Add(entry);
        }

        Visit(target);
        return ordered;
    }
}

/// <summary>
/// Read-only plugin catalog used by CLI list/info/help and target resolution.
/// It never creates directories, processes pending operations, loads assemblies, or opens the database.
/// </summary>
internal sealed class PluginManifestCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly string _pluginsDirectory;

    public PluginManifestCatalog(string? pluginsDirectory = null)
    {
        _pluginsDirectory = pluginsDirectory ?? Path.Combine(AppContext.BaseDirectory, "plugins");
    }

    public PluginCatalogSnapshot Read()
    {
        var entries = new Dictionary<string, PluginCatalogEntry>(StringComparer.Ordinal);
        var diagnostics = new List<string>();

        ReadRoot(_pluginsDirectory, entries, diagnostics);
        var extraPath = Environment.GetEnvironmentVariable(PluginLoader.ExtraPluginEnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(extraPath)
            && !string.Equals(Path.GetFullPath(extraPath), Path.GetFullPath(_pluginsDirectory), StringComparison.OrdinalIgnoreCase))
        {
            ReadRoot(extraPath, entries, diagnostics);
        }

        return new PluginCatalogSnapshot(entries, diagnostics);
    }

    private static void ReadRoot(
        string root,
        Dictionary<string, PluginCatalogEntry> entries,
        List<string> diagnostics)
    {
        if (!Directory.Exists(root)) return;

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(root).ToArray();
        }
        catch (Exception exception)
        {
            diagnostics.Add($"Cannot enumerate plugin directory '{root}': {exception.Message}");
            return;
        }

        foreach (var directory in directories.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(Path.GetFileName(directory), ".pending", StringComparison.OrdinalIgnoreCase))
                continue;

            var manifestPath = Path.Combine(directory, "plugin.json");
            if (!File.Exists(manifestPath)) continue;

            try
            {
                var manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestPath), JsonOptions);
                if (manifest is null || string.IsNullOrWhiteSpace(manifest.PluginId))
                    throw new InvalidDataException("pluginId is required.");

                var info = ToPluginInfo(manifest, directory);
                var (index, indexError) = ReadCliIndex(directory, manifest);
                var entry = new PluginCatalogEntry(info, manifestPath, index, indexError);
                if (!entries.TryAdd(info.PluginId, entry))
                    diagnostics.Add($"Duplicate plugin id '{info.PluginId}' at '{manifestPath}'.");
            }
            catch (Exception exception)
            {
                diagnostics.Add($"Invalid plugin manifest '{manifestPath}': {exception.Message}");
            }
        }
    }

    private static (PluginCliIndex? Index, string? Error) ReadCliIndex(string directory, PluginManifest manifest)
    {
        var path = Path.Combine(directory, "plugin.cli.json");
        if (!File.Exists(path)) return (null, null);

        try
        {
            var index = JsonSerializer.Deserialize<PluginCliIndex>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidDataException("The sidecar is empty.");
            if (index.SchemaVersion != 1)
                throw new InvalidDataException($"Unsupported schemaVersion {index.SchemaVersion}.");
            if (!string.Equals(index.PluginId, manifest.PluginId, StringComparison.Ordinal))
                throw new InvalidDataException("pluginId does not match plugin.json.");
            if (!string.IsNullOrWhiteSpace(manifest.Version)
                && !string.Equals(index.PluginVersion, manifest.Version, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("pluginVersion does not match plugin.json.");
            if (string.IsNullOrWhiteSpace(index.Alias))
                throw new InvalidDataException("alias is required.");
            if (index.GetRuntimeProfile() == PluginCliRuntimeProfile.Selected
                && !string.Equals(index.RuntimeProfile, "selected", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("runtimeProfile must be 'selected' or 'data'.");
            return (index, null);
        }
        catch (Exception exception)
        {
            return (null, $"Invalid CLI sidecar '{path}': {exception.Message}");
        }
    }

    private static PluginInfo ToPluginInfo(PluginManifest manifest, string directory)
    {
        return new PluginInfo
        {
            PluginId = manifest.PluginId!,
            Name = manifest.Name ?? manifest.PluginId!,
            Version = manifest.Version ?? "1.0.0",
            Author = manifest.Author ?? string.Empty,
            Description = manifest.Description ?? string.Empty,
            Dependencies = manifest.Dependencies ?? [],
            SharedAssemblies = manifest.SharedAssemblies ?? [],
            InstallPath = directory,
            AssemblyPath = string.IsNullOrWhiteSpace(manifest.Assembly)
                ? string.Empty
                : Path.Combine(directory, manifest.Assembly),
            State = Enum.TryParse<PluginState>(manifest.State, out var state) ? state : PluginState.Installed,
            InstallTime = manifest.InstallTime,
            IsBuiltIn = manifest.IsBuiltIn,
            HasMetadata = true,
            MinPluginSdkVersion = manifest.MinPluginSdkVersion,
            Kind = string.IsNullOrWhiteSpace(manifest.Kind) ? "Avalonia" : manifest.Kind,
            Web = manifest.Web
        };
    }
}
