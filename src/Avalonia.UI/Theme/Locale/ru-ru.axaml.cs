using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Avalonia.UI.Theme.Locale;

public class ru_ru: ResourceDictionary
{
    public ru_ru()
    {
        AvaloniaXamlLoader.Load(this);
        this["STRING_PAGINATION_PAGE"] = string.Empty;
    }
}
