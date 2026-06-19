using Avalonia.Plugin.TDLSharp.Resources;
using Avalonia.Plugin.TDLSharp.ViewModels;
using Ursa.Controls;

namespace Avalonia.Plugin.TDLSharp.Services;

/// <summary>
/// Shows the login dialog as an overlay. Returns true if the user authenticated successfully.
/// </summary>
public static class LoginDialogService
{
    /// <summary>
    /// Show the login dialog. Returns true if authentication succeeded, false otherwise.
    /// </summary>
    public static async Task<bool> ShowLoginDialogAsync()
    {
        var vm = new LoginViewModel();
        var options = new OverlayDialogOptions
        {
            Title = Strings.Get("LOGIN_Title"),
            CanResize = false,
            CanLightDismiss = false,
            IsCloseButtonVisible = true,
            HorizontalAnchor = HorizontalPosition.Center,
            VerticalAnchor = VerticalPosition.Center,
        };
        var result = await OverlayDialog.ShowCustomAsync<Controls.LoginDialog, LoginViewModel, bool>(vm, options: options);
        return result is true;
    }
}
