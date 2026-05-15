using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Avalonia.UI.Theme.Locale;

public class en_us: ResourceDictionary
{
    public en_us()
    {
        AvaloniaXamlLoader.Load(this);
        this["STRING_PAGINATION_PAGE"] = string.Empty;
    }
}