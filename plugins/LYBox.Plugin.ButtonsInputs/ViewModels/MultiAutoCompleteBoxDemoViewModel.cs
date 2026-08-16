using System.Collections.ObjectModel;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Services;
using LYBox.Plugin.ButtonsInputs.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Controls;
using LYBox.Plugin.Shared.Models;

namespace LYBox.Plugin.ButtonsInputs.ViewModels;

[NavigationItem("MultiAutoCompleteBox")]
[Menu("NAV_MultiAutoCompleteBox", "MultiAutoCompleteBox", "NAV_ButtonsInputs")]
[ViewMap(typeof(MultiAutoCompleteBoxDemo))]
public class MultiAutoCompleteBoxDemoViewModel : ObservableObject
{
    public ObservableCollection<ControlData> Items { get; set; }
    public ObservableCollection<ControlData> SelectedItems { get; set; }
    public AutoCompleteFilterPredicate<object> FilterPredicate { get; set; }

    public MultiAutoCompleteBoxDemoViewModel()
    {
        SelectedItems = new ObservableCollection<ControlData>();
        // 演示数据统一来自共享 ControlCatalog（O-10），消除跨 ViewModel 硬编码重复
        Items = new ObservableCollection<ControlData>(ControlCatalog.GetAll());
        FilterPredicate = Search;
    }

    private static bool Search(string? text, object? data)
    {
        if (text is null) return true;
        if (data is not ControlData control) return false;
        return control.MenuHeader.Contains(text, StringComparison.InvariantCultureIgnoreCase) ||
               control.Chinese.Contains(text, StringComparison.InvariantCultureIgnoreCase);
    }
}





