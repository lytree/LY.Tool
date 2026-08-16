using System.Collections.ObjectModel;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Models;
using LYBox.Plugin.Shared.Services;
using LYBox.Plugin.ButtonsInputs.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.ButtonsInputs.ViewModels;

[NavigationItem("AutoCompleteBox")]
[Menu("NAV_AutoCompleteBox", "AutoCompleteBox", "NAV_ButtonsInputs")]
[ViewMap(typeof(AutoCompleteBoxDemo))]
public class AutoCompleteBoxDemoViewModel : ObservableObject
{
    public AutoCompleteBoxDemoViewModel()
    {
        // 演示数据统一来自共享 ControlCatalog（O-10），消除跨 ViewModel 硬编码重复
        Controls = new ObservableCollection<ControlData>(ControlCatalog.GetAll());
    }

    public ObservableCollection<ControlData> Controls { get; set; }
}





