using System.Text.Json;
using LYBox.Plugin.Downloader.Models;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;

namespace LYBox.Plugin.Downloader.Services;

/// <summary>
/// 二进制路径与全局设置的持久化。
/// 优先通过宿主 <see cref="ISettingsService"/>（SQLite 持久化，统一在设置页管理）；
/// 若 <see cref="ServiceLocator"/> 尚未初始化（如插件早期初始化阶段），回退到本地 JSON 文件。
/// JSON 路径：%LOCALAPPDATA%/LYBox/DownloaderPlugin/settings.json。
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

    /// <summary>
    /// 当前设置（每次访问都从 ISettingsService 读取最新值，确保设置页修改立即生效）。
    /// 若 ServiceLocator 不可用则回退到 JSON。
    /// </summary>
    public static BinaryPaths Current => Load();

    private static ISettingsService? TryGetSettingsService()
        => ServiceLocator.TryGetService(out ISettingsService? svc) ? svc : null;

    /// <summary>从 ISettingsService 读取设置；服务不可用时回退到 JSON。</summary>
    public static BinaryPaths Load()
    {
        var svc = TryGetSettingsService();
        if (svc != null)
        {
            return new BinaryPaths
            {
                FfmpegPath = svc.GetValue<string>("DL.FfmpegPath") ?? string.Empty,
                Mp4DecryptPath = svc.GetValue<string>("DL.Mp4DecryptPath") ?? string.Empty,
                MkvmergePath = svc.GetValue<string>("DL.MkvmergePath") ?? string.Empty,
                ShakaPackagerPath = svc.GetValue<string>("DL.ShakaPackagerPath") ?? string.Empty,
                Proxy = svc.GetValue<string>("DL.Proxy"),
                UseSystemProxy = svc.GetValue<bool>("DL.UseSystemProxy"),
                LogLevel = svc.GetValue<string>("DL.LogLevel") ?? "INFO",
            };
        }
        return LoadFromJson();
    }

    /// <summary>保存设置到 ISettingsService；服务不可用时回退到 JSON。</summary>
    public static void Save(BinaryPaths cfg)
    {
        var svc = TryGetSettingsService();
        if (svc != null)
        {
            svc.SetValue("DL.FfmpegPath", cfg.FfmpegPath);
            svc.SetValue("DL.Mp4DecryptPath", cfg.Mp4DecryptPath);
            svc.SetValue("DL.MkvmergePath", cfg.MkvmergePath);
            svc.SetValue("DL.ShakaPackagerPath", cfg.ShakaPackagerPath);
            svc.SetValue("DL.Proxy", cfg.Proxy);
            svc.SetValue("DL.UseSystemProxy", cfg.UseSystemProxy);
            svc.SetValue("DL.LogLevel", cfg.LogLevel);
            return;
        }
        SaveToJson(cfg);
    }

    private static BinaryPaths LoadFromJson()
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

    private static void SaveToJson(BinaryPaths cfg)
    {
        try
        {
            var json = JsonSerializer.Serialize(cfg, JsonOpts);
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* 持久化失败不阻塞 UI */ }
    }
}
