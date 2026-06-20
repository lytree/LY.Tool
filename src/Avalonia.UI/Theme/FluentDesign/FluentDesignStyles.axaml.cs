using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Avalonia.UI.Theme.FluentDesign;

public class FluentDesignStyles : Styles
{
    public FluentDesignStyles()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
