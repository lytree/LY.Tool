using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace LYBox.Plugin.TDLSharp.Controls;

public partial class ScriptLogPanel : UserControl
{
    public static readonly StyledProperty<IEnumerable?> LogEntriesProperty =
        AvaloniaProperty.Register<ScriptLogPanel, IEnumerable?>(nameof(LogEntries));

    public static readonly StyledProperty<ICommand?> ClearLogCommandProperty =
        AvaloniaProperty.Register<ScriptLogPanel, ICommand?>(nameof(ClearLogCommand));

    public IEnumerable? LogEntries
    {
        get => GetValue(LogEntriesProperty);
        set => SetValue(LogEntriesProperty, value);
    }

    public ICommand? ClearLogCommand
    {
        get => GetValue(ClearLogCommandProperty);
        set => SetValue(ClearLogCommandProperty, value);
    }

    public ScriptLogPanel()
    {
        InitializeComponent();
    }
}
