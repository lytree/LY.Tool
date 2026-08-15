using System.Threading.Tasks;
using Avalonia.Controls;
using Ursa.Controls;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;
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
        return new MainWindow()
        {
            DataContext = new MainWindowViewModel()
        };
    }
}
