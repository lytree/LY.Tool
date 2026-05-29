
using Avalonia.Controls;
using Avalonia.Plugin.Shared.ViewModels;
using System.Collections.ObjectModel;

namespace Avalonia.Plugin.Shared;


public interface IPlugin
{
    Task InitializeAsync() => Task.CompletedTask;
    Task ShutdownAsync() => Task.CompletedTask;
    IEnumerable<KeyValuePair<Type, ViewFactory>> GetViewDefinitions();
    Dictionary<string, ViewModelFactory> GetNavigationItems();
    List<KeyValuePair<string?, MenuItemViewModel>> GetMenuItems();
}


/// <summary>
/// ViewModel 工厂委托
/// </summary>
public delegate object ViewModelFactory();
/// <summary>
/// 视图工厂委托
/// </summary>
public delegate Control ViewFactory();




/// <summary>
/// 工具栏项视图模型
/// </summary>
public class ToolBarItemViewModel
{
    public string Content { get; set; }
    public object Command { get; set; }
    public object OverflowMode { get; set; }
}

/// <summary>
/// 工具栏分隔符视图模型
/// </summary>
public class ToolBarSeparatorViewModel : ToolBarItemViewModel
{
}

/// <summary>
/// 工具栏按钮项视图模型
/// </summary>
public class ToolBarButtonItemViewModel : ToolBarItemViewModel
{
}

/// <summary>
/// 工具栏复选框项视图模型
/// </summary>
public class ToolBarCheckBoxItemViweModel : ToolBarItemViewModel
{
    public bool IsChecked { get; set; }
}

/// <summary>
/// 工具栏组合框项视图模型
/// </summary>
public class ToolBarComboBoxItemViewModel : ToolBarItemViewModel
{
    public object SelectedItem { get; set; }
    public object Items { get; set; }
}
