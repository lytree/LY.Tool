using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Ursa.Controls;
using LYBox.Plugin.Shared;
using LYBox.UrsaWindow.ViewModels;
using LYBox.UrsaWindow.Services;

namespace LYBox.UrsaWindow.Views;

public partial class MvvmSplashWindow : SplashWindow
{
    private bool _transitioned;

    public MvvmSplashWindow()
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

    protected override void OnDataContextChanged(EventArgs e)
    {
        // 不调用 base.OnDataContextChanged，避免 Ursa 2.1.x 基类双重订阅 RequestClose
        if (DataContext is SplashViewModel vm)
        {
            vm.RequestClose += OnSplashRequestClose;
        }
    }

    private void OnSplashRequestClose(object? sender, object? e)
    {
        if (_transitioned)
            return;

        _transitioned = true;

        if (DataContext is SplashViewModel vm)
        {
            vm.RequestClose -= OnSplashRequestClose;
        }

        Dispatcher.Post(async () =>
        {
            try
            {
                var nextWindow = await CreateNextWindow();
                if (nextWindow is not null)
                {
                    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        desktop.MainWindow = nextWindow;
                    }
                    nextWindow.Show();
                    Close();
                }
            }
            catch (Exception ex)
            {
                // 过渡失败时记录异常并尝试直接显示主窗口
                System.Diagnostics.Debug.WriteLine($"Splash transition failed: {ex}");
                Console.Error.WriteLine($"[SplashTransition] {ex}");
                try
                {
                    var navSvc = ServiceLocator.GetService<INavigationService>();
                    var menuSvc = ServiceLocator.GetService<IMenuConfigurationService>();
                    var fallback = new MainWindow
                    {
                        DataContext = new MainViewViewModel(navSvc!, menuSvc!)
                    };
                    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                        desktop.MainWindow = fallback;
                    fallback.Show();
                    Close();
                }
                catch
                {
                    // 最终 fallback 也失败，至少不永远卡在闪屏
                    Close();
                }
            }
        });
    }

    private static IClassicDesktopStyleApplicationLifetime? ApplicationLifetime =>
        Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
}
