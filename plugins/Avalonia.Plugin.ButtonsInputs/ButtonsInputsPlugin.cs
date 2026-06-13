using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.Shared.Services;
using Avalonia.Plugin.ButtonsInputs.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace Avalonia.Plugin.ButtonsInputs;

[GenerateMetadata]
public partial class ButtonsInputsPlugin : IPluginMetadata
{
    public string Name => "Buttons & Inputs Plugin";
    public string Version => "1.0.0";

    public string Author => "";

    public string Description => "";

    public IEnumerable<string> Dependencies => [];

    public string PluginId => "0F2F7DB6-0E9B-D872-442F-2CBC3DAC1F56";

    public Task InitializeAsync(IServiceCollection services) => Task.CompletedTask;

    public Task RegisterAsync(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService<ILocalizationService>() is { } loc)
            loc.RegisterResourceManager(Strings.ResourceManager);
        return Task.CompletedTask;
    }

    // public Dictionary<string, ViewModelFactory> GetNavigationItems()
    // {
    //     var navigationItems = new Dictionary<string, ViewModelFactory>
    //     {
    //         {"ButtonGroup", () => new ButtonGroupDemoViewModel() },
    //         {"IconButton", () => new IconButtonDemoViewModel() },
    //         {"AutoCompleteBox", () => new AutoCompleteBoxDemoViewModel() },
    //         {"ClassInput", () => new ClassInputDemoViewModel() },
    //         {"EnumSelector", () => new EnumSelectorDemoViewModel() },
    //         {"Form", () => new FormDemoViewModel() },
    //         {"KeyGestureInput", () => new KeyGestureInputDemoViewModel() },
    //         {"IpBox", () => new IPv4BoxDemoViewModel() },
    //         {"MultiComboBox", () => new MultiComboBoxDemoViewModel() },
    //         {"MultiAutoCompleteBox", () => new MultiAutoCompleteBoxDemoViewModel() },
    //         {"NumericUpDown", () => new NumericUpDownDemoViewModel() },
    //         {"NumPad", () => new NumPadDemoViewModel() },
    //         {"PathPicker", () => new PathPickerDemoViewModel() },
    //         {"PinCode", () => new PinCodeDemoViewModel() },
    //         {"RangeSlider", () => new RangeSliderDemoViewModel() },
    //         {"Rating", () => new RatingDemoViewModel() },
    //         {"SelectionList", () => new SelectionListDemoViewModel() },
    //         {"TagInput", () => new TagInputDemoViewModel() },
    //         {"ThemeToggler", () => new ThemeTogglerDemoViewModel() },
    //         {"TreeComboBox", () => new TreeComboBoxDemoViewModel() },
    //     };

    //     return navigationItems;
    // }

    // public IEnumerable<(string? ParentKey, MenuItemViewModel MenuItem)> GetMenuItems()
    // {
    //     var menuItems = new List<(string? ParentKey, MenuItemViewModel MenuItem)>();

    //     var buttonsAndInputs = new MenuItemViewModel
    //     {
    //         MenuHeader = "Buttons & Inputs",
    //         Children = new()
    //         {
    //             new() { MenuHeader = "Button Group", Key = "ButtonGroup" },
    //             new() { MenuHeader = "Icon Button", Key = "IconButton", Status = "Updated" },
    //             new() { MenuHeader = "AutoCompleteBox", Key = "AutoCompleteBox" },
    //             new() { MenuHeader = "Class Input", Key = "ClassInput" },
    //             new() { MenuHeader = "Enum Selector", Key = "EnumSelector" },
    //             new() { MenuHeader = "Form", Key = "Form" },
    //             new() { MenuHeader = "KeyGestureInput", Key = "KeyGestureInput" },
    //             new() { MenuHeader = "IPv4Box", Key = "IpBox" },
    //             new() { MenuHeader = "MultiComboBox", Key = "MultiComboBox", Status = "Updated" },
    //             new() { MenuHeader = "Multi AutoCompleteBox", Key = "MultiAutoCompleteBox" },
    //             new() { MenuHeader = "Numeric UpDown", Key = "NumericUpDown" },
    //             new() { MenuHeader = "NumPad", Key = "NumPad" },
    //             new() { MenuHeader = "PathPicker", Key = "PathPicker", Status = "Updated" },
    //             new() { MenuHeader = "PinCode", Key = "PinCode" },
    //             new() { MenuHeader = "RangeSlider", Key = "RangeSlider" },
    //             new() { MenuHeader = "Rating", Key = "Rating" },
    //             new() { MenuHeader = "Selection List", Key = "SelectionList" },
    //             new() { MenuHeader = "TagInput", Key = "TagInput" },
    //             new() { MenuHeader = "Theme Toggler", Key = "ThemeToggler" },
    //             new() { MenuHeader = "TreeComboBox", Key = "TreeComboBox", Status = "Updated" },
    //         }
    //     };
    //     menuItems.Add((null, buttonsAndInputs));

    //     return menuItems;
    // }



    // public IEnumerable<KeyValuePair<Type, ViewFactory>> GetViewDefinitions()
    // {
    //     yield return new(typeof(ButtonGroupDemoViewModel), () => new ButtonGroupDemo());
    // }
}



