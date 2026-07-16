using System.Threading.Tasks;
using Avalonia.Controls;
using Ursa.Controls;
using LYBox.Plugin.Shared;
using LYBox.Layout.Ursa.ViewModels;
using LYBox.Layout.Core.Services;
using LYBox.Layout.Ursa.Services;

namespace LYBox.Layout.Ursa.Views;

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
