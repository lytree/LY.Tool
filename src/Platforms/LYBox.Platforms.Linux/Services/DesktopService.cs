using Avalonia.Helpers;
using LYBox.Platforms.Abstraction.Services;
using Avalonia.Shared;
using Microsoft.Extensions.Logging;

namespace LYBox.Platforms.Linux.Services;

public class DesktopService : IDesktopService
{
    private static string StartupPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config/autostart/cn.Avalonia.app.desktop");
    
    public bool IsAutoStartEnabled
    {
        get =>
            File.Exists(StartupPath);
        set
        {
            try
            {
                var startupPath = StartupPath;
                if (!Path.Exists(Path.GetDirectoryName(startupPath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(startupPath) ?? "");
                }
                if (value)
                {
                    _ = ShortcutHelpers.CreateFreedesktopShortcutAsync(startupPath);
                } else if (File.Exists(startupPath))
                {
                    File.Delete(startupPath);
                }
            }
            catch (Exception e)
            {
                IAppHost.TryGetService<ILogger<DesktopService>>()?.LogError(e, "无法设置自启动项目：{} ({})", StartupPath, value);
            }
        }
    }

    public bool IsUrlSchemeRegistered
    {
        get => false;set{} 
    }
}