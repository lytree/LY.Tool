using Avalonia.Core;
using Avalonia.Helpers;
using LYBox.Platforms.Abstraction.Services;
using WindowsShortcutFactory;

namespace Avalonia.Platform.Windows.Services;

public class DesktopService : IDesktopService
{
    private readonly string _startupShortcutPath;
    private bool? _cachedAutoStart;

    public DesktopService()
    {
        _startupShortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Avalonia.lnk");
    }

    public bool IsAutoStartEnabled
    {
        get => _cachedAutoStart ??= File.Exists(_startupShortcutPath);
        set
        {
            if (value)
            {
                using var shortcut = new WindowsShortcut();
                shortcut.Path = AppBase.ExecutingEntrance;
                shortcut.WorkingDirectory = Path.GetDirectoryName(AppBase.ExecutingEntrance);
                shortcut.Save(_startupShortcutPath);
            }
            else
            {
                if (File.Exists(_startupShortcutPath))
                    File.Delete(_startupShortcutPath);
            }
            _cachedAutoStart = value;
        }
    }
    public bool IsUrlSchemeRegistered{
        get => UriProtocolRegisterHelper.IsRegistered();
        set
        {
            if (value)
            {
                UriProtocolRegisterHelper.Register();
            }
            else
            {
                UriProtocolRegisterHelper.UnRegister();
            }
        }
    }
}