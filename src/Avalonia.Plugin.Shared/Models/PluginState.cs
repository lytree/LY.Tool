namespace Avalonia.Plugin.Shared.Models;

public enum PluginState
{
    NotInstalled,
    Installed,
    Discovered,
    Loaded,
    Registered,
    Disabled,
    PendingUninstall,
    Error
}
