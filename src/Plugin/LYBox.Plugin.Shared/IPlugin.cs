
using Avalonia.Controls;
using LYBox.Plugin.Shared.ViewModels;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace LYBox.Plugin.Shared;


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
}


/// <summary>
/// ViewModel 工厂委托
/// </summary>
public delegate object ViewModelFactory();
/// <summary>
/// 视图工厂委托
/// </summary>
public delegate Control ViewFactory();
