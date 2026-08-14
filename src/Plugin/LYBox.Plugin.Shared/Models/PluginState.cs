namespace LYBox.Plugin.Shared.Models;

public enum PluginState
{
    NotInstalled,
    Installed,
    Loaded,
    Disabled,
    PendingUninstall,
    PendingUpgrade,
    Error
}
