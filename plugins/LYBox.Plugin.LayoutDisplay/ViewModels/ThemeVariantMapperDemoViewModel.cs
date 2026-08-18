using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.LayoutDisplay.Pages;

namespace LYBox.Plugin.LayoutDisplay.ViewModels;

[NavigationItem("KeyThemeVariantMapper")]
[Menu("NAV_ThemeVariantMapper", "KeyThemeVariantMapper", "NAV_LayoutDisplay")]
[ViewMap(typeof(ThemeVariantMapperDemo))]
public partial class ThemeVariantMapperDemoViewModel : ViewModelBase
{
    [ObservableProperty] private ThemeVariant? _requestedThemeVariant = ThemeVariant.Default;

    [RelayCommand]
    private void SetLight() => RequestedThemeVariant = ThemeVariant.Light;

    [RelayCommand]
    private void SetDark() => RequestedThemeVariant = ThemeVariant.Dark;

    [RelayCommand]
    private void SetDefault() => RequestedThemeVariant = ThemeVariant.Default;
}
