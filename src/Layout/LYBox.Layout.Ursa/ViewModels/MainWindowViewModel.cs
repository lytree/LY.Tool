using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;
using LYBox.Layout.Core.Services;
using LYBox.Layout.Ursa.Services;

namespace LYBox.Layout.Ursa.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public MainViewViewModel MainViewViewModel { get; set; }

    public MainWindowViewModel()
    {
        var navigationService = ServiceLocator.GetService<INavigationService>();
        var menuConfigurationService = ServiceLocator.GetService<IMenuConfigurationService>();
        MainViewViewModel = new MainViewViewModel(navigationService!, menuConfigurationService!);
    }
}
