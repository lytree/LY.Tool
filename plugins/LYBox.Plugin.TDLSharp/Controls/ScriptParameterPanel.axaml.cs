using System.Collections;
using Avalonia;
using Avalonia.Controls;

namespace LYBox.Plugin.TDLSharp.Controls;

public partial class ScriptParameterPanel : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ParametersProperty =
        AvaloniaProperty.Register<ScriptParameterPanel, IEnumerable?>(nameof(Parameters));

    public IEnumerable? Parameters
    {
        get => GetValue(ParametersProperty);
        set => SetValue(ParametersProperty, value);
    }

    public ScriptParameterPanel()
    {
        InitializeComponent();
    }
}
