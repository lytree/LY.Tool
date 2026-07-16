using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LYBox.FluentWindow.Views;

namespace LYBox.FluentWindow;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (Program.HasArg("--acrylic"))
            {
                var window = new DemoMainWindow { StartWithAcrylic = true };
                desktop.MainWindow = window;
            }
            else if (Program.HasArg("--dialog"))
            {
                var window = new DemoMainWindow { StartAsDialog = true };
                desktop.MainWindow = window;
            }
            else
            {
                desktop.MainWindow = new DemoMainWindow();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
