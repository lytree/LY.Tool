using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Avalonia.UI.Theme.Locale;

public class de_de: ResourceDictionary
{
    public de_de()
    {
        AvaloniaXamlLoader.Load(this);
        this["STRING_PAGINATION_PAGE"] = string.Empty;
    }
}