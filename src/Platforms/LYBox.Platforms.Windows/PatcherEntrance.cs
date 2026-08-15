using HarmonyLib;

namespace Avalonia.Platform.Windows;

public static class PatcherEntrance
{
    public static void InstallPatchers()
    {
        var harmony = new Harmony("cn.Avalonia.app.patchers");
        harmony.PatchAll();
    }
}