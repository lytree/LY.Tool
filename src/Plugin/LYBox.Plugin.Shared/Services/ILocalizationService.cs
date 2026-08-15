using System.Globalization;
using System.Resources;

namespace LYBox.Plugin.Shared.Services;

public interface ILocalizationService
{
    CultureInfo CurrentCulture { get; }

    string GetString(string key);

    string GetString(string key, string fallback);

    string GetString(string key, params object[] args);

    void SetCulture(CultureInfo culture);

    void RegisterResourceManager(ResourceManager manager, string prefix = "");

    event EventHandler<CultureInfo>? CultureChanged;
}
