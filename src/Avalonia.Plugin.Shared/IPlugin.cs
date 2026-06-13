
using Avalonia.Controls;
using Avalonia.Plugin.Shared.ViewModels;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace Avalonia.Plugin.Shared;


public interface IPlugin
{
    /// <summary>
    /// 初始化插件，向 IServiceCollection 注册服务。在 DI 容器构建前调用。
    /// </summary>
    Task InitializeAsync(IServiceCollection services) => Task.CompletedTask;

    /// <summary>
    /// DI 容器构建完成后调用，用于注册语言资源、设置等需要 IServiceProvider 的操作
    /// </summary>
    Task RegisterAsync(IServiceProvider serviceProvider) => Task.CompletedTask;

    Task ShutdownAsync() => Task.CompletedTask;
    IEnumerable<KeyValuePair<Type, ViewFactory>> GetViewDefinitions();
    Dictionary<string, ViewModelFactory> GetNavigationItems();
    List<KeyValuePair<string?, MenuItemViewModel>> GetMenuItems();

    /// <summary>
    /// 获取插件提供的图标资源字典，用于菜单图标等
    /// </summary>
    IResourceDictionary? GetIconResources() => null;
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
