using System.Threading.Tasks;
using Avalonia.Controls;
using Ursa.Controls;
using LYBox.Plugin.Shared;
using LYBox.UrsaWindow.ViewModels;
using LYBox.UrsaWindow.Services;

namespace LYBox.UrsaWindow.Views;

public partial class MainSplashWindow : SplashWindow
{
    public MainSplashWindow()
    {
        InitializeComponent();
    }

    protected override async Task<Window?> CreateNextWindow()
    {
        var navigationService = ServiceLocator.GetService<INavigationService>();
        var menuConfigurationService = ServiceLocator.GetService<IMenuConfigurationService>();
        return new MainWindow()
        {
            DataContext = new MainViewViewModel(navigationService!, menuConfigurationService!)
        };
    }
}
