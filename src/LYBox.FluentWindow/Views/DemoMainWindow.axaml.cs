using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace LYBox.FluentWindow.Views;

public partial class DemoMainWindow : LYBox.FluentWindow.Windowing.FluentWindow
{
    private bool _acrylicEnabled;
    private bool _micaEnabled;

    public bool StartWithAcrylic { get; set; }
    public bool StartAsDialog { get; set; }

    public DemoMainWindow()
    {
        InitializeComponent();

        if (StartWithAcrylic)
        {
            _acrylicEnabled = true;
            Background = new SolidColorBrush(Color.FromArgb(220, 243, 243, 243));
        }

        if (StartAsDialog)
        {
            ShowAsDialog = true;
            Title = "FluentWindow (Dialog Mode)";
            Width = 500;
            Height = 400;
        }
    }

    private void OpenDialog_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new LYBox.FluentWindow.Windowing.FluentWindow
        {
            Title = "Dialog Window",
            Width = 400,
            Height = 300,
            ShowAsDialog = true,
            TitleBarContent = new TextBlock { Text = "Dialog Demo", VerticalAlignment = VerticalAlignment.Center },
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new TextBlock
            {
                Text = "This is a dialog window.",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20)
            }
        };
        dialog.ShowDialog(this);
    }

    private void ToggleSidebar_Click(object? sender, RoutedEventArgs e)
    {
        // Placeholder: toggle sidebar visibility in a real app
    }

    private async void OpenRepository_Click(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.Launcher is not null)
            await top.Launcher.LaunchUriAsync(new System.Uri("https://github.com"));
    }

    private void ToggleTheme_Click(object? sender, RoutedEventArgs e)
    {
        var app = Application.Current;
        if (app is null) return;
        app.RequestedThemeVariant = app.ActualThemeVariant == ThemeVariant.Dark
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
    }

    private void ToggleAcrylic_Click(object? sender, RoutedEventArgs e)
    {
        _acrylicEnabled = !_acrylicEnabled;
        if (_acrylicEnabled)
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 243, 243, 243));
        }
        else
        {
            Background = Brushes.White;
        }
    }

    private void ToggleMica_Click(object? sender, RoutedEventArgs e)
    {
        _micaEnabled = !_micaEnabled;
        if (_micaEnabled)
        {
            Background = new SolidColorBrush(Color.FromArgb(250, 238, 238, 238));
        }
        else
        {
            Background = Brushes.White;
        }
    }

    private void OpenSplash_Click(object? sender, RoutedEventArgs e)
    {
        var splash = new LYBox.FluentWindow.Windowing.FluentWindow
        {
            Title = "Splash Screen",
            Width = 500,
            Height = 300,
            TitleBarIsVisible = false,
            TitleBarContentIsVisible = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
                Child = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "FluentWindow Demo",
                            Foreground = Brushes.White,
                            FontSize = 28,
                            FontWeight = FontWeight.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = "Splash Screen",
                            Foreground = Brushes.LightGray,
                            FontSize = 14,
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    }
                }
            }
        };
        splash.Show();
    }
}
