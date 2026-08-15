using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Core.Abstractions.Services;
using FluentLYBox.UrsaWindow.Windowing;
using HarmonyLib;

namespace Avalonia.Platform.Windows.Patches;

[HarmonyPatch(typeof(AppWindow), "InitializeAppWindow")]
public class AppWindowInitializeAppWindowPatcher
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_PseudoClasses")]
    private static extern IPseudoClasses GetPseudoClasses(StyledElement window);
    
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_IsWindows")]
    private static extern void SetIsWindowsProperty(AppWindow window, bool v);
    
    static void Postfix(AppWindow __instance)
    {
        if (!IThemeService.UseNativeTitlebar) return;
        GetPseudoClasses(__instance).Remove(":windows");
        SetIsWindowsProperty(__instance, false);
    }
}