# 插件 API 参考文档
本参考详细描述 `LYBox.Plugin.Shared` 命名空间下插件开发者可用的全部 API。所有 API 都位于 NuGet 包 `LYBox.Plugin.Shared` 中，插件通过 `PrivateAssets="all"` 引用，版本由 `$(PluginSdkVersion)` 统一管理。

---

## 目录

- [1. 核心接口](#1-核心接口)
  - [1.1 IPlugin](#11-iplugin)
  - [1.2 IPluginMetadata](#12-ipluginmetadata)
- [2. 元数据生成器（源生成器）](#2-元数据生成器源生成器)
  - [2.1 GenerateMetadataAttribute](#21-generatemetadataattribute)
  - [2.2 ViewMapAttribute](#22-viewmapattribute)
  - [2.3 NavigationItemAttribute](#23-navigationitemattribute)
  - [2.4 MenuAttribute](#24-menuattribute)
- [3. 基础设施类](#3-基础设施类)
  - [3.1 ServiceLocator](#31-servicelocator)
  - [3.2 ViewLocator](#32-viewlocator)
  - [3.3 ViewModelBase](#33-viewmodelbase)
- [4. 视图模型](#4-视图模型)
  - [4.1 MenuItemViewModel](#41-menuitemviewmodel)
  - [4.2 ToolBarItemViewModel 系列工具栏项](#42-toolbaritemviewmodel-系列工具栏项)
- [5. 模型](#5-模型)
  - [5.1 PluginManifest](#51-pluginmanifest)
  - [5.2 PluginInfo](#52-plugininfo)
  - [5.3 PluginState（枚举）](#53-pluginstate枚举)
  - [5.4 SettingDefinition / SettingItem / SettingType](#54-settingdefinition--settingitem--settingtype)
- [6. 服务接口](#6-服务接口)
  - [6.1 ILocalizationService](#61-ilocalizationservice)
  - [6.2 IPluginLoader](#62-ipluginloader)
  - [6.3 IPluginInstallationManager](#63-iplugininstallationmanager)
  - [6.4 ISettingsService](#64-isettingsservice)
  - [6.5 ITaskRegistry](#65-itaskregistry)
  - [6.6 IWindowInfoService](#66-iwindowinfoservice)
- [7. MSBuild 属性与目标](#7-msbuild-属性与目标)
  - [7.1 .csproj 元数据属性](#71-csproj-元数据属性)
  - [7.2 自动生成的 plugin.json 清单](#72-自动生成的-pluginjson-清单)
  - [7.3 共享程序集转发](#73-共享程序集转发)
- [8. 完整插件实现示例](#8-完整插件实现示例)

---

## 1. 核心接口

### 1.1 IPlugin

```csharp
namespace LYBox.Plugin.Shared;

public interface IPlugin
{
    Task InitializeAsync(IServiceCollection services) => Task.CompletedTask;
    Task RegisterAsync(IServiceProvider serviceProvider) => Task.CompletedTask;
    Task ShutdownAsync() => Task.CompletedTask;
    IEnumerable<KeyValuePair<Type, ViewFactory>> GetViewDefinitions();
    Dictionary<string, ViewModelFactory> GetNavigationItems();
    List<KeyValuePair<string?, MenuItemViewModel>> GetMenuItems();
    IResourceDictionary? GetIconResources() => null;
}
```

**实现方式**：通常**不直接实现**。使用 `[GenerateMetadata]` 标注元数据类（见 §2.1），由源生成器自动生成 `IPlugin` 实现。

| 方法 | 调用时机 | 用途 |
|------|---------|------|
| `InitializeAsync(services)` | DI 容器构建**前** | 注册服务到 `IServiceCollection` |
| `RegisterAsync(serviceProvider)` | DI 容器构建**后** | 注册本地化资源、订阅事件等需要 `IServiceProvider` 的操作 |
| `ShutdownAsync()` | 应用关闭时 | 释放资源、保存状态 |
| `GetViewDefinitions()` | UI 渲染时 | 返回 ViewModel→View 工厂映射（由 `[ViewMap]` 自动生成） |
| `GetNavigationItems()` | 导航初始化时 | 返回 key→ViewModel 工厂映射（由 `[NavigationItem]` 自动生成） |
| `GetMenuItems()` | 菜单构建时 | 返回菜单项列表（由 `[Menu]` 自动生成） |
| `GetIconResources()` | 资源加载时 | 返回插件图标资源字典 |

**委托类型**：

```csharp
public delegate object ViewModelFactory();  // 创建 ViewModel 实例
public delegate Control ViewFactory();       // 创建 View 实例
```

---

### 1.2 IPluginMetadata

```csharp
namespace LYBox.Plugin.Shared;

public interface IPluginMetadata
{
    string Name { get; }
    string Version { get; }
    string Author { get; }
    string Description { get; }
    IEnumerable<string> Dependencies { get; }
    string PluginId { get; }
    string MinPluginSdkVersion => "0.0.0";
}
```

| 属性 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `Name` | `string` | 是 | 插件显示名（人类可读） |
| `Version` | `string` | 是 | SemVer 版本号（如 `"1.0.0"`） |
| `Author` | `string` | 是 | 作者 |
| `Description` | `string` | 是 | 一句话描述 |
| `Dependencies` | `IEnumerable<string>` | 是 | 依赖的插件 PluginId 列表（可为空） |
| `PluginId` | `string` | 是 | 全局唯一 ID，建议用 GUID（如 `"b5eab285-8673-4991-a45a-b43bee2cb840"`） |
| `MinPluginSdkVersion` | `string` | 否 | 所需最低 SDK 契约版本，默认 `"0.0.0"`（无约束） |

> **PluginId 必须全局唯一**。Host 启动时检查重复，重复的 PluginId 会导致后加载的插件被拒绝。建议用 `Guid.NewGuid()` 生成并固化在代码中。

---

## 2. 元数据生成器（源生成器）

通过 `LYBox.Plugin.Generators` NuGet 包（analyzer 引用）提供。生成器扫描 `[GenerateMetadata]` 标注的类，自动实现 `IPlugin` 接口。

### 2.1 GenerateMetadataAttribute

```csharp
namespace LYBox.Plugin.Shared.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class GenerateMetadataAttribute : Attribute { }
```

**用法**：标注在实现 `IPluginMetadata` 的 partial 类上。生成器会生成该类的 `IPlugin` 实现（`GetViewDefinitions`、`GetNavigationItems`、`GetMenuItems` 由 `[ViewMap]`、`[NavigationItem]`、`[Menu]` 推导）。

```csharp
[GenerateMetadata]
public partial class MyPlugin : IPluginMetadata
{
    public string Name => "My Plugin";
    public string Version => "1.0.0";
    public string Author => "Me";
    public string Description => "Does something useful";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "your-unique-guid-here";
}
```

> **重要**：类必须声明为 `partial`，且实现 `IPluginMetadata`。否则生成器无法注入生成的 `IPlugin` 实现。

---

### 2.2 ViewMapAttribute

```csharp
namespace LYBox.Plugin.Shared.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ViewMapAttribute(Type viewType) : Attribute
{
    public Type ViewType { get; } = viewType;
}
```

**用法**：标注在 ViewModel 类上，建立 ViewModel→View 映射，让 `IPlugin.GetViewDefinitions()` 自动包含该项。

```csharp
[ViewMap(typeof(MyPageView))]
public class MyPageViewModel : ViewModelBase
{
    // ...
}
```

> Host 的全局 `ViewLocator` 会用此映射在 `ContentControl.Content="{Binding ViewModel}"` 时自动解析 View。

---

### 2.3 NavigationItemAttribute

```csharp
namespace LYBox.Plugin.Shared.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class NavigationItemAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}
```

**用法**：标注在 ViewModel 类上（通常与 `[ViewMap]` 同时使用），将其注册为可导航项。

```csharp
[ViewMap(typeof(MyPageView))]
[NavigationItem("my-plugin:main")]
public class MyPageViewModel : ViewModelBase { ... }
```

> 导航 key 建议使用 `插件名:页面名` 格式避免冲突。导航通过 `INavigationService.NavigateTo("my-plugin:main")` 触发。

---

### 2.4 MenuAttribute

```csharp
namespace LYBox.Plugin.Shared.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class MenuAttribute(string header, string key, string? parentKey = null) : Attribute
{
    public string Header { get; set; } = header;
    public string Key { get; set; } = key;
    public string? ParentKey { get; set; } = parentKey;
    public string? IconName { get; set; }
    public string? Status { get; set; } = null;
    public int Order { get; set; } = 0;
}
```

| 属性 | 类型 | 说明 |
|------|------|------|
| `header` | `string` | 菜单标题（也作为本地化 key，由 `ILocalizationService` 解析） |
| `key` | `string` | 菜单项唯一键（通常与 `[NavigationItem]` 的 key 相同） |
| `parentKey` | `string?` | 父菜单项 key，用于构建层级。`null` 表示顶层菜单 |
| `IconName` | `string?` | 图标资源键（在 `GetIconResources()` 返回的资源字典中查找） |
| `Status` | `string?` | 状态标记（如 `"New"`、`"Beta"`，UI 可显示徽章） |
| `Order` | `int` | 同级菜单项排序（默认 0，越小越靠前） |

**用法**：

```csharp
[ViewMap(typeof(MyPageView))]
[NavigationItem("my-plugin:main")]
[Menu("我的插件", "my-plugin:main", IconName = "MyPluginIcon", Order = 100)]
public class MyPageViewModel : ViewModelBase { ... }

// 子菜单
[Menu("子功能", "my-plugin:sub", parentKey: "my-plugin:main")]
public class MySubPageViewModel : ViewModelBase { ... }
```

> Host 用 `MenuItemTreeBuilder.BuildTree()` 将扁平 `MenuItemViewModel` 列表解析为树。

---

## 3. 基础设施类

### 3.1 ServiceLocator

```csharp
namespace LYBox.Plugin.Shared;

public static class ServiceLocator
{
    public static void Initialize(IServiceProvider serviceProvider);
    public static IServiceProvider GetServiceProvider();
    public static T GetService<T>() where T : class;
    public static bool TryGetService<T>(out T? service) where T : class;
}
```

**Host 在 `App.Initialize()` 中调用 `Initialize(provider)` 一次**。插件代码通过它访问 Host 注册的服务。

| 方法 | 行为 |
|------|------|
| `Initialize(provider)` | 设置内部静态 provider。重复调用会覆盖 |
| `GetServiceProvider()` | 返回 provider；未初始化抛 `InvalidOperationException` |
| `GetService<T>()` | 解析服务；未注册抛 `InvalidOperationException` |
| `TryGetService<T>(out service)` | 安全版本；未注册返回 `false`，`service` 为 null |

**推荐用法**：

```csharp
// 优先用 TryGetService 检查
if (ServiceLocator.TryGetService<ILocalizationService>(out var loc))
{
    loc.RegisterResourceManager(Strings.ResourceManager);
}

// 确定存在的服务可直接获取
var nav = ServiceLocator.GetService<INavigationService>();
nav.NavigateTo("my-plugin:main");
```

---

### 3.2 ViewLocator

```csharp
namespace LYBox.Plugin.Shared;

public class ViewLocator : IDataTemplate
{
    public bool SupportsRecycling => false;
    public Control? Build(object? data);
    public bool Match(object? data);
}
```

全局 `IDataTemplate`，在 `App.axaml` 资源中注册为 `ViewLocator`。VM 类型名为 `XxxViewModel` 时查找 `Xxx` 或 `XxxView` 类型的 View。缓存通过 `ConditionalWeakTable` 实现，VM→View 循环无内存泄漏。

**XAML 用法**：

```xml
<!-- 自动解析 Content 为对应 View -->
<ContentControl Content="{Binding CurrentPage}" />
```

无需在插件代码中显式调用 `ViewLocator`。

---

### 3.3 ViewModelBase

```csharp
namespace LYBox.Plugin.Shared;

public class ViewModelBase : ObservableObject, IDisposable
{
    public bool IsDisposed { get; }
    public virtual void Dispose();
}
```

继承自 `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`，所有插件 ViewModel 应继承此类。提供 `IDisposable` 模式（`IsDisposed` 标志 + `Dispose()` 虚方法）。

```csharp
public class MyPageViewModel : ViewModelBase
{
    private int _count;

    [ObservableProperty]
    private string _name = string.Empty;

    [RelayCommand]
    private void Increment() => _count++;

    public override void Dispose()
    {
        // 释放事件订阅、定时器等
        base.Dispose();
    }
}
```

> **MVVM Toolkit 强制规则**：
> - **属性**：用 `[ObservableProperty]` 自动生成 INPC，**禁止**手写 `private T _field; public T Foo { get => _field; set => SetProperty(ref _field, value); }`
> - **命令**：用 `[RelayCommand]` 自动生成 `ICommand`，**禁止**手写 `RelayCommand`/`DelegateCommand` 实例
> - **类声明**：partial VM 类必须标注 `[INotifyPropertyChanged]` 或继承 `ObservableObject`

---

## 4. 视图模型

### 4.1 MenuItemViewModel

```csharp
namespace LYBox.Plugin.Shared.ViewModels;

public enum ControlStatus { New, Beta, Stable }

public class MenuItemViewModel : ViewModelBase
{
    public string? MenuHeader { get; set; }      // 本地化标题（自动解析）
    public string? RawHeader { get; }            // 原始未本地化标题
    public string? MenuIconName { get; set; }    // 图标资源键
    public string? Key { get; set; }             // 唯一键
    public string? Status { get; set; }          // 状态标记
    public string? Group { get; set; }           // 分组
    public int Order { get; set; }               // 排序
    public bool IsSeparator { get; set; }        // 是否分隔符
    public ObservableCollection<MenuItemViewModel> Children { get; set; }
    public ICommand ActivateCommand { get; set; }

    public void RefreshHeader();                 // 重新解析本地化标题
}
```

**导航行为**：`ActivateCommand` 默认通过 `WeakReferenceMessenger` 发送 `key` 到 `"JumpTo"` 通道，由 `NavigationService` 接收并跳转。

**本地化**：`MenuHeader` setter 自动调用 `ILocalizationService.GetString(header)` 解析本地化字符串。语言切换时调用 `RefreshHeader()` 刷新整个菜单树。

---

### 4.2 ToolBarItemViewModel 系列工具栏项

```csharp
public class ToolBarItemViewModel
{
    public string Content { get; set; }
    public object Command { get; set; }
    public object OverflowMode { get; set; }
}

public class ToolBarSeparatorViewModel : ToolBarItemViewModel { }
public class ToolBarButtonItemViewModel : ToolBarItemViewModel { }

public class ToolBarCheckBoxItemViweModel : ToolBarItemViewModel
{
    public bool IsChecked { get; set; }
}

public class ToolBarComboBoxItemViewModel : ToolBarItemViewModel
{
    public object SelectedItem { get; set; }
    public object Items { get; set; }
}
```

> 注意：`ToolBarCheckBoxItemViweModel` 类名拼写为 `ViweModel`（已知 typo），保留以保持向后兼容。

---

## 5. 模型

### 5.1 PluginManifest

```csharp
namespace LYBox.Plugin.Shared.Models;

public class PluginManifest
{
    public string? PluginId { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string? Assembly { get; set; }
    public List<string>? Dependencies { get; set; }
    public List<string>? SharedAssemblies { get; set; }    // 额外共享程序集模式
    public string? State { get; set; }
    public DateTime? InstallTime { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? MinPluginSdkVersion { get; set; }
}
```

由 `GeneratePluginManifest` MSBuild target 自动生成 `plugin.json` 文件，与插件 DLL 同目录。Host 加载时读取此清单判断元数据与依赖。

---

### 5.2 PluginInfo

```csharp
namespace LYBox.Plugin.Shared.Models;

public class PluginInfo
{
    // 包含 PluginManifest 的全部字段，外加：
    public PluginState State { get; set; }
    public string? AssemblyPath { get; set; }
    public string? ManifestPath { get; set; }
    public IPlugin? Instance { get; set; }
    public AssemblyLoadContext? LoadContext { get; set; }
}
```

---

### 5.3 PluginState（枚举）

```csharp
namespace LYBox.Plugin.Shared.Models;

public enum PluginState
{
    NotInstalled,
    Installed,
    Loaded,
    Disabled,
    PendingUninstall
}
```

**状态流转**：

```
NotInstalled ──install──> Installed ──load──> Loaded
                              │                  │
                              │                  └──disable──> Disabled
                              │                                  │
                              │                                  └──enable──> Loaded
                              │
                              └──uninstall──> PendingUninstall ──purge──> (removed)
```

状态变化通过 `IPluginInstallationManager` 的事件通知 UI 更新。

---

### 5.4 SettingDefinition / SettingItem / SettingType

```csharp
public enum SettingType
{
    String, Integer, Boolean, Color, Enum, Custom
}

public class SettingItem
{
    public string Key { get; set; }
    public string DisplayName { get; set; }
    public SettingType Type { get; set; }
    public object? DefaultValue { get; set; }
    public object? Value { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
}

public class SettingDefinition
{
    public string Key { get; set; }
    public string DisplayName { get; set; }
    public SettingType Type { get; set; }
    public object? DefaultValue { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public IEnumerable<string>? Options { get; set; }   // Enum 类型的可选项
}
```

插件在 `InitializeAsync` 中通过 `ISettingsService.RegisterDefinition(...)` 注册自定义设置项，Host 会在设置页面自动渲染。

---

## 6. 服务接口

### 6.1 ILocalizationService

```csharp
namespace LYBox.Plugin.Shared.Services;

public interface ILocalizationService
{
    void RegisterResourceManager(ResourceManager manager);
    string GetString(string key);
    string GetString(string key, params object[] args);
    event EventHandler? CultureChanged;
}
```

**用法**：插件在 `RegisterAsync` 中注册本地化资源：

```csharp
public Task RegisterAsync(IServiceProvider serviceProvider)
{
    if (serviceProvider.GetService<ILocalizationService>() is { } loc)
        loc.RegisterResourceManager(Strings.ResourceManager);
    return Task.CompletedTask;
}
```

资源文件命名：`Strings.resx`（默认）、`Strings.zh-CN.resx`（中文）等。Host 会按当前 UI 文化堆叠查询所有已注册 `ResourceManager`。

---

### 6.2 IPluginLoader

```csharp
namespace LYBox.Plugin.Shared.Services;

public interface IPluginLoader
{
    Task<IEnumerable<PluginInfo>> LoadPluginsAsync(string directory);
    Task<PluginInfo?> LoadPluginAsync(string manifestPath);
    event EventHandler<PluginLoadedEventArgs>? PluginLoaded;
    event EventHandler<PluginUnloadedEventArgs>? PluginUnloaded;
}
```

> 插件一般不直接调用此接口。Host 在 `App.Initialize()` 中自动调用扫描 `{AppBaseDir}/plugins/` 和 `AVALONIA_EXTRA_PLUGINS_PATH` 环境变量路径。

---

### 6.3 IPluginInstallationManager

```csharp
namespace LYBox.Plugin.Shared.Services;

public interface IPluginInstallationManager
{
    Task<bool> InstallAsync(string pluginZipPath);
    Task<bool> UninstallAsync(string pluginId);
    Task<bool> EnableAsync(string pluginId);
    Task<bool> DisableAsync(string pluginId);
    IEnumerable<PluginInfo> GetInstalledPlugins();
    event EventHandler<PluginStateChangedEventArgs>? PluginStateChanged;
}
```

**用法**：插件可调用此接口查看其他插件状态或动态启停（需具备相应权限）。

```csharp
var mgr = ServiceLocator.GetService<IPluginInstallationManager>();
foreach (var p in mgr.GetInstalledPlugins())
{
    Console.WriteLine($"{p.Name} ({p.Version}) - {p.State}");
}
```

---

### 6.4 ISettingsService

```csharp
namespace LYBox.Plugin.Shared.Services;

public interface ISettingsService
{
    void RegisterDefinition(SettingDefinition definition);
    T? Get<T>(string key);
    void Set<T>(string key, T value);
    event EventHandler<SettingChangedEventArgs>? SettingChanged;
}
```

**用法**：插件注册自定义设置项后，Host 设置页面自动渲染对应控件。

```csharp
public Task InitializeAsync(IServiceCollection services)
{
    // 注册插件设置定义（在 DI 构建前不可调用 ISettingsService，
    // 应在 RegisterAsync 中注册）
    return Task.CompletedTask;
}

public Task RegisterAsync(IServiceProvider serviceProvider)
{
    var settings = serviceProvider.GetService<ISettingsService>();
    settings?.RegisterDefinition(new SettingDefinition
    {
        Key = "my-plugin:enable-feature-x",
        DisplayName = "启用特性 X",
        Type = SettingType.Boolean,
        DefaultValue = false,
        Description = "开启后可使用 X 功能",
        Category = "MyPlugin"
    });
    return Task.CompletedTask;
}

// 读取设置
var enabled = ServiceLocator.GetService<ISettingsService>().Get<bool>("my-plugin:enable-feature-x");
```

---

### 6.5 ITaskRegistry

```csharp
namespace LYBox.Plugin.Shared.Services;

public interface ITaskRegistry
{
    Guid Register(string name, Func<CancellationToken, Task> taskFactory);
    void Unregister(Guid taskId);
    IEnumerable<RunningTask> GetRunningTasks();
    event EventHandler<TaskStateChangedEventArgs>? TaskStateChanged;
}
```

**用法**：注册长时间运行的后台任务，Host 在 UI 中显示进度并支持取消。

```csharp
var registry = ServiceLocator.GetService<ITaskRegistry>();
var taskId = registry.Register("MyPlugin: Process data", async ct =>
{
    for (int i = 0; i < 100; i++)
    {
        ct.ThrowIfCancellationRequested();
        await Task.Delay(100, ct);
        // 进度更新...
    }
});

// 取消
registry.Unregister(taskId);
```

---

### 6.6 IWindowInfoService

```csharp
namespace LYBox.Plugin.Shared.Services;

public interface IWindowInfoService
{
    Window? GetMainWindow();
    IEnumerable<Window> GetAllWindows();
    event EventHandler<WindowEventArgs>? WindowOpened;
    event EventHandler<WindowEventArgs>? WindowClosed;
}
```

**用法**：插件需要访问主窗口（如弹出对话框的 Owner）时使用。

```csharp
var windowSvc = ServiceLocator.GetService<IWindowInfoService>();
var mainWnd = windowSvc.GetMainWindow();
var dialog = new MyDialog { WindowStartupLocation = WindowStartupLocation.CenterOwner };
await dialog.ShowDialog(mainWnd);
```

---

## 7. MSBuild 属性与目标

### 7.1 .csproj 元数据属性

| 属性 | 必填 | 默认值 | 说明 |
|------|------|--------|------|
| `<PluginId>` | 是 | — | 全局唯一插件 ID（建议 GUID） |
| `<PluginName>` | 是 | — | 显示名 |
| `<PluginAuthor>` | 是 | — | 作者 |
| `<PluginDescription>` | 是 | — | 一句话描述 |
| `<PluginVersion>` | 否 | `1.0.0` | SemVer 版本号 |
| `<Version>` | 否 | `$(PluginVersion)` | .NET 程序集版本（通常与 PluginVersion 一致） |
| `<MinPluginSdkVersion>` | 否 | `0.0.0` | 所需最低 SDK 契约版本 |
| `<AvaloniaUseCompiledBindingsByDefault>` | 推荐 | `false` | **必须设为 `true`** |

**模板**：

```xml
<PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Library</OutputType>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>

    <PluginId>b5eab285-8673-4991-a45a-b43bee2cb840</PluginId>
    <PluginName>My Plugin</PluginName>
    <PluginAuthor>Me</PluginAuthor>
    <PluginDescription>Does something useful</PluginDescription>
    <PluginVersion>1.0.0</PluginVersion>
    <Version>$(PluginVersion)</Version>
    <AssemblyVersion>$(PluginVersion)</AssemblyVersion>
    <FileVersion>$(PluginVersion)</FileVersion>
    <MinPluginSdkVersion>1.0.0</MinPluginSdkVersion>
</PropertyGroup>

<ItemGroup>
    <PackageReference Include="LYBox.Plugin.Generators" Version="$(PluginSdkVersion)"
                      OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <PackageReference Include="LYBox.Plugin.Shared" Version="$(PluginSdkVersion)"
                      PrivateAssets="all" />
</ItemGroup>
```

> `$(PluginSdkVersion)` 在 `Directory.Build.props` 中统一定义，确保插件与 Host 使用相同 SDK 版本。

---

### 7.2 自动生成的 plugin.json 清单

由 `GeneratePluginManifest` target（在 `LYBox.Plugin.Shared.targets` 中定义）在编译时生成，输出到 `bin/$(Configuration)/$(TargetFramework)/plugin.json`。内容示例：

```json
{
  "pluginId": "b5eab285-8673-4991-a45a-b43bee2cb840",
  "name": "My Plugin",
  "version": "1.0.0",
  "author": "Me",
  "description": "Does something useful",
  "assembly": "MyPlugin.dll",
  "dependencies": [],
  "minPluginSdkVersion": "1.0.0",
  "isBuiltIn": false
}
```

**禁用自动生成**（不推荐）：

```xml
<GeneratePluginManifest>false</GeneratePluginManifest>
```

---

### 7.3 共享程序集转发

每个插件加载在独立的可回收 `AssemblyLoadContext` 中。下列程序集通过 `_SharedAssembliesPatterns` 属性转发到 Host 的默认 context，**所有插件共享同一份**：

- `System.*`、`System.Private.Uri`、`System.Reactive`
- `Microsoft.Bcl.AsyncInterfaces`
- `Avalonia`、`Avalonia.*`
- `SkiaSharp`、`SkiaSharp.*`、`HarfBuzzSharp.*`、`MicroCom.Runtime`
- `LYBox.Plugin.Shared`
- `CommunityToolkit.*`
- `Microsoft.Extensions.DependencyInjection`（及 `Abstractions`、`Options`、`Primitives`、`Logging.Abstractions`）
- `Irihi.*`、`Ursa`、`Semi.Avalonia`

**插件声明额外共享程序集**：在 `plugin.json` 的 `sharedAssemblies` 字段添加条目（精确名或 `前缀*` 模式）：

```json
{
  "sharedAssemblies": ["MySharedLib", "MyCompany.*"]
}
```

> 仅声明真正需要跨插件共享的程序集。无关程序集应保持插件本地，避免版本冲突。

**禁用程序集排除**（不推荐，仅供调试）：

```xml
<LYBoxPluginSharedExclusionsEnabled>false</LYBoxPluginSharedExclusionsEnabled>
```

---

## 8. 完整插件实现示例

### 8.1 Plugin.cs

```csharp
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using MyPlugin.Resources;

namespace MyPlugin;

[GenerateMetadata]
public partial class MyPlugin : IPluginMetadata
{
    public string Name => "My Plugin";
    public string Version => "1.0.0";
    public string Author => "Me";
    public string Description => "Demonstrates plugin system with Fluent UI styles.";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "b5eab285-8673-4991-a45a-b43bee2cb840";
    public string MinPluginSdkVersion => "1.0.0";

    public Task InitializeAsync(IServiceCollection services)
    {
        // 注册插件自己的服务
        services.AddSingleton<MyDataService>();
        return Task.CompletedTask;
    }

    public Task RegisterAsync(IServiceProvider serviceProvider)
    {
        // 注册本地化资源
        if (serviceProvider.GetService<ILocalizationService>() is { } loc)
            loc.RegisterResourceManager(Strings.ResourceManager);

        // 注册设置项
        if (serviceProvider.GetService<ISettingsService>() is { } settings)
        {
            settings.RegisterDefinition(new SettingDefinition
            {
                Key = "my-plugin:enable-feature",
                DisplayName = "启用特性 X",
                Type = SettingType.Boolean,
                DefaultValue = false,
                Category = "MyPlugin"
            });
        }

        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        // 释放资源
        return Task.CompletedTask;
    }

    // GetViewDefinitions / GetNavigationItems / GetMenuItems 由源生成器自动实现
    // （扫描 [ViewMap] / [NavigationItem] / [Menu] 标注的类）

    public IResourceDictionary? GetIconResources()
    {
        // 返回包含插件图标的资源字典（如菜单图标）
        return null;  // 无自定义图标时返回 null
    }
}
```

### 8.2 ViewModel

```csharp
using LYBox.Plugin.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyPlugin.Views;

namespace MyPlugin.ViewModels;

[ViewMap(typeof(MyPageView))]
[NavigationItem("my-plugin:main")]
[Menu("My Plugin", "my-plugin:main", IconName = "MyPluginIcon", Order = 100)]
public partial class MyPageViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _welcomeMessage = "Hello from MyPlugin!";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private string _inputText = string.Empty;

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        await Task.Delay(500);
        // 业务逻辑
    }

    private bool CanSubmit() => !string.IsNullOrWhiteSpace(InputText);
}
```

### 8.3 View（XAML）

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:u="using:Irihi.Ursa.Controls"
             xmlns:vm="clr-namespace:MyPlugin.ViewModels"
             x:Class="MyPlugin.Views.MyPageView"
             x:DataType="vm:MyPageViewModel">

    <ScrollViewer Padding="24">
        <StackPanel Spacing="16" MaxWidth="800">

            <!-- 页面标题（Fluent 排版） -->
            <StackPanel Spacing="4">
                <TextBlock Classes="WinUILargeTitle" Text="{Binding WelcomeMessage}" />
                <TextBlock Classes="WinUIPageDescription" Text="演示 Fluent UI 样式用法" />
            </StackPanel>

            <!-- 信息提示（FluentInfoBar） -->
            <Border Classes="FluentInfoBar FluentInfoBarSeverityInformational">
                <Grid ColumnDefinitions="Auto,*,Auto">
                    <PathIcon Grid.Column="0"
                              Data="{DynamicResource FluentIcon20FilledInfo}"
                              Width="20" Height="20"
                              Foreground="{DynamicResource FluentAccentBrush}" />
                    <StackPanel Grid.Column="1" Margin="12,0">
                        <TextBlock Classes="FluentInfoBarTitle" Text="提示" />
                        <TextBlock Classes="FluentInfoBarMessage"
                                   Text="在下方输入数据并提交。" />
                    </StackPanel>
                </Grid>
            </Border>

            <!-- 设置卡片（FluentSettingsExpander） -->
            <Expander Classes="FluentSettingsExpander">
                <Expander.Header>
                    <Grid ColumnDefinitions="Auto,*,Auto">
                        <Border Grid.Column="0" Classes="FluentSettingsCardIconHost">
                            <PathIcon Data="{DynamicResource FluentIcon24RegularEdit}"
                                      Width="20" Height="20" />
                        </Border>
                        <StackPanel Grid.Column="1" Margin="12,0" VerticalAlignment="Center">
                            <TextBlock Classes="FluentSettingsCardTitle" Text="输入数据" />
                            <TextBlock Classes="FluentSettingsCardDescription"
                                       Text="点击展开填写表单" />
                        </StackPanel>
                    </Grid>
                </Expander.Header>
                <Expander.Content>
                    <StackPanel Spacing="12">
                        <TextBox Text="{Binding InputText}"
                                 Watermark="请输入内容..."
                                 MinWidth="300" />
                        <Button Classes="FluentAccent"
                                Content="提交"
                                Command="{Binding SubmitCommand}" />
                    </StackPanel>
                </Expander.Content>
            </Expander>

            <!-- 链接按钮（FluentHyperlinkButton） -->
            <TextBlock Classes="WinUIBody">
                需要帮助？请访问
                <Button Classes="FluentHyperlinkButton"
                        Command="{Binding ShowHelpCommand}">
                    在线文档
                </Button>
            </TextBlock>

        </StackPanel>
    </ScrollViewer>
</UserControl>
```

---

## 附录：版本契约

`PluginSdkContract.CurrentVersion` 是当前 SDK 契约版本字符串。Host 启动时与每个插件 `MinPluginSdkVersion` 比对：插件要求版本 > 当前 SDK 版本则拒绝加载并记录日志。

| Plugin SDK 版本 | 兼容 Host 版本 | 主要变化 |
|----------------|----------------|---------|
| 1.0.0 | 当前 | 初始版本 |

> 升级 Plugin SDK 版本号时，应同步更新 `Directory.Build.props` 中的 `PluginSdkVersion` 并重新构建 `artifacts/packages/sdk/` 下的 NuGet 包（`.\build.ps1 --build=bin`）。
