using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace LYBox.Layout.Fluent.Controls;

public class ButtonItemsPanel : ItemsControl 
{
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<ButtonItemsPanel, ICommand?>(nameof(Command));

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
}
