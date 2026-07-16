using System.Text.Json;

namespace LYBox.UrsaWindow.Services;

/// <summary>
/// 插件系统共享工具：统一 JsonSerializerOptions 与文件系统操作，消除 PluginLoader 与 PluginInstallationManager 间的重复代码。
/// </summary>
internal static class PluginUtilities
{
    /// <summary>
    /// 统一的 JSON 序列化选项：camelCase 命名 + 缩进输出，用于 plugin.json 和 .upgrade.json。
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 递归复制目录及其所有子目录和文件。
    /// </summary>
    public static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var dest = Path.GetFullPath(Path.Combine(destDir, relative));
            var dir = Path.GetDirectoryName(dest);
            if (dir != null) Directory.CreateDirectory(dir);
            File.Copy(file, dest, overwrite: true);
        }
    }
}
