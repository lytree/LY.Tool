namespace LYBox.Layout.Core.Services;

internal static class PluginPathValidator
{
    private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static void ValidatePluginId(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            throw new ArgumentException("Plugin id is required", nameof(pluginId));

        if (pluginId is "." or ".." ||
            !string.Equals(pluginId, pluginId.TrimEnd(' ', '.'), StringComparison.Ordinal) ||
            Path.IsPathRooted(pluginId) ||
            pluginId.Contains('/') ||
            pluginId.Contains('\\') ||
            pluginId.Contains(':') ||
            pluginId.Any(char.IsControl) ||
            pluginId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            WindowsReservedNames.Contains(pluginId.Split('.')[0]))
        {
            throw new ArgumentException("Plugin id contains invalid path characters", nameof(pluginId));
        }
    }

    public static string GetDirectChildPath(string rootDirectory, string pluginId)
    {
        ValidatePluginId(pluginId);

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
        var candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(root, pluginId)));
        var parent = Path.GetDirectoryName(candidate);
        if (!string.Equals(parent, root, PathComparison))
            throw new ArgumentException("Plugin id must resolve to a direct child of the plugin directory", nameof(pluginId));

        return candidate;
    }

    public static bool IsWithinDirectory(string rootDirectory, string candidatePath)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory))
            + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(candidatePath);
        return candidate.StartsWith(root, PathComparison);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
