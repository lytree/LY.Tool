using System.Reflection;
using System.Text.Json;
using LYBox.Plugin.Shared.Models;

namespace LYBox.Layout.Core.Services;

internal static class PluginInventoryCatalog
{
    public static IReadOnlyDictionary<string, PluginInfo> ReadManagedPlugins(string? pluginsDirectory = null)
    {
        var root = Path.GetFullPath(pluginsDirectory ?? Path.Combine(AppContext.BaseDirectory, "plugins"));
        var plugins = new Dictionary<string, PluginInfo>(StringComparer.Ordinal);
        if (!Directory.Exists(root))
            return plugins;

        foreach (var directory in Directory.EnumerateDirectories(root).OrderBy(path => path, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(directory);
            if (name.StartsWith(".", StringComparison.Ordinal) ||
                name.EndsWith(".new", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".old", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            TryAddManifestPlugin(directory, plugins);
        }

        ApplyPendingUpgradeState(root, plugins);
        return plugins;
    }

    public static IReadOnlyDictionary<string, PluginInfo> ReadExternalPlugins()
    {
        var extraPath = Environment.GetEnvironmentVariable(PluginLoader.ExtraPluginEnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(extraPath))
            return new Dictionary<string, PluginInfo>();

        var root = Path.GetFullPath(extraPath);
        var plugins = new Dictionary<string, PluginInfo>(StringComparer.Ordinal);
        if (!Directory.Exists(root))
            return plugins;

        if (!TryAddManifestPlugin(root, plugins))
        {
            foreach (var dllPath in Directory.EnumerateFiles(root, "*.dll", SearchOption.TopDirectoryOnly))
                TryAddAssemblyPlugin(dllPath, plugins);
        }

        foreach (var directory in Directory.EnumerateDirectories(root).OrderBy(path => path, StringComparer.Ordinal))
        {
            if (TryAddManifestPlugin(directory, plugins))
                continue;

            var candidate = Path.Combine(directory, $"{Path.GetFileName(directory)}.dll");
            if (File.Exists(candidate))
                TryAddAssemblyPlugin(candidate, plugins);
        }

        return plugins;
    }

    private static bool TryAddManifestPlugin(
        string directory,
        IDictionary<string, PluginInfo> plugins)
    {
        var manifestPath = Path.Combine(directory, "plugin.json");
        if (!File.Exists(manifestPath))
            return false;

        try
        {
            var manifest = JsonSerializer.Deserialize<PluginManifest>(
                File.ReadAllText(manifestPath),
                PluginUtilities.JsonOptions);
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.PluginId))
                return false;

            PluginPathValidator.ValidatePluginId(manifest.PluginId);
            if (plugins.ContainsKey(manifest.PluginId))
                return true;

            var assemblyPath = ResolveAssemblyPath(directory, manifest.Assembly);
            var state = Enum.TryParse<PluginState>(manifest.State, out var parsedState)
                ? parsedState
                : PluginState.Installed;
            if (state == PluginState.Loaded)
                state = PluginState.Installed;

            plugins.Add(manifest.PluginId, PluginManifestMapper.FromManifest(
                manifest,
                assemblyPath,
                hasMetadata: true,
                installPath: Path.GetFullPath(directory),
                state: state,
                isBuiltIn: manifest.IsBuiltIn));
            return true;
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException or
                InvalidDataException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static void TryAddAssemblyPlugin(
        string assemblyPath,
        IDictionary<string, PluginInfo> plugins)
    {
        try
        {
            var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
            var pluginId = assemblyName.Name ?? Path.GetFileNameWithoutExtension(assemblyPath);
            if (string.IsNullOrWhiteSpace(pluginId) || plugins.ContainsKey(pluginId))
                return;

            plugins.Add(pluginId, PluginManifestMapper.FromAssembly(
                assemblyName,
                Path.GetFullPath(assemblyPath),
                pluginIdFallback: Path.GetFileNameWithoutExtension(assemblyPath),
                name: pluginId,
                installPath: Path.GetDirectoryName(Path.GetFullPath(assemblyPath)) ?? string.Empty));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
        }
    }

    private static string ResolveAssemblyPath(string directory, string? assembly)
    {
        if (string.IsNullOrWhiteSpace(assembly) || Path.IsPathRooted(assembly))
            return string.Empty;

        var candidate = Path.GetFullPath(Path.Combine(directory, assembly));
        if (!PluginPathValidator.IsWithinDirectory(directory, candidate))
            throw new InvalidDataException("Plugin assembly path escapes its plugin directory.");
        return candidate;
    }

    private static void ApplyPendingUpgradeState(
        string pluginsDirectory,
        IDictionary<string, PluginInfo> plugins)
    {
        var pendingRoot = Path.Combine(pluginsDirectory, ".pending");
        if (!Directory.Exists(pendingRoot))
            return;

        foreach (var markerPath in Directory.EnumerateFiles(pendingRoot, "*.upgrade.json"))
        {
            try
            {
                var pending = JsonSerializer.Deserialize<PendingUpgradeInfo>(
                    File.ReadAllText(markerPath),
                    PluginUtilities.JsonOptions);
                if (pending is null || !plugins.TryGetValue(pending.PluginId, out var plugin))
                    continue;

                plugins[pending.PluginId] = plugin.WithPendingUpgrade(
                    pending.NewVersion,
                    $"Upgrade to v{pending.NewVersion} scheduled; restart to apply.") with
                {
                    State = PluginState.PendingUpgrade
                };
            }
            catch (Exception exception) when (
                exception is JsonException or IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
