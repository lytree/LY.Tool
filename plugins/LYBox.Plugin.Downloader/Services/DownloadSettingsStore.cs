using System.Text.Json;
using LYBox.Plugin.Downloader.Models;

namespace LYBox.Plugin.Downloader.Services;

/// <summary>
/// 二进制路径与全局设置的 JSON 持久化。
/// 存储位置：%LOCALAPPDATA%/LYBox/DownloaderPlugin/settings.json（跨平台用 LocalApplicationData）。
/// </summary>
public static class DownloadSettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string SettingsDir
    {
        get
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(baseDir, "LYBox", "DownloaderPlugin");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string SettingsPath => Path.Combine(SettingsDir, "settings.json");

    /// <summary>当前内存中的设置（启动时加载，修改后调用 Save 持久化）</summary>
    public static BinaryPaths Current { get; private set; } = Load();

    public static BinaryPaths Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var cfg = JsonSerializer.Deserialize<BinaryPaths>(json, JsonOpts);
                return cfg ?? new BinaryPaths();
            }
        }
        catch { /* 损坏文件回退默认 */ }
        return new BinaryPaths();
    }

    public static void Save(BinaryPaths cfg)
    {
        Current = cfg;
        try
        {
            var json = JsonSerializer.Serialize(cfg, JsonOpts);
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* 持久化失败不阻塞 UI */ }
    }
}
