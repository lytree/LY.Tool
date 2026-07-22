using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Ursa.Controls;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;
using LYBox.Layout.Ursa.ViewModels;
using LYBox.Layout.Core.Services;
using LYBox.Layout.Ursa.Services;

namespace LYBox.Layout.Ursa.Views;

public partial class MvvmSplashWindow : SplashWindow
{
    private bool _transitioned;
    private Window? _nextWindow;

    public MvvmSplashWindow()
    {
        InitializeComponent();
    }

    protected override async Task<Window?> CreateNextWindow()
    {
        // 幂等保护：基类 CountDown 与子类 RequestClose 可能同时触发，
        // 确保仅创建一个 MainWindow 实例，避免双窗口。
        if (_nextWindow is not null)
            return null;
        // DataContext 必须是 MainWindowViewModel：MainWindow.axaml 的 x:DataType=MainWindowViewModel，
        // 且 <MainView DataContext="{Binding MainViewViewModel}" /> 依赖此层级。
        // MainWindowViewModel 构造函数内部通过 ServiceLocator 自行创建 MainViewViewModel。
        _nextWindow = new MainWindow()
        {
            DataContext = new MainWindowViewModel()
        };
        return _nextWindow;
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
                    // 先隐藏闪屏再显示新窗口，避免两个窗口同时可见
                    Hide();
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
                        var fallback = new MainWindow
                        {
                            DataContext = new MainWindowViewModel()
                        };
                        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                            desktop.MainWindow = fallback;
                        Hide();
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
