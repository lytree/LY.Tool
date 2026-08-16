---
name: lybox-plugin
description: "LYBox 非 Web 插件（Avalonia 原生插件）开发规范：csproj 声明、GenerateMetadata 源生成器、ViewMap/NavigationItem/Menu 特性、本地化与设置注册、生命周期约束。新建或修改 plugins/ 下不涉及 WebView/wwwroot 的插件时使用。"
risk: unknown
source: project
date_added: "2026-08-16"
---

# LYBox 非 Web 插件（Avalonia 原生）开发规范

> 适用范围：`plugins/` 下**不含** WebView、wwwroot、RPC 命令的插件。
> Web 插件（含 WebView / 前端页面）请使用 `lybox-web-plugin` skill。
> 本规范基于当前代码库事实；标 ⏳ 的条目为 `docs/WebHost-Optimization-Design.md` 中的**设计中**变更，实施前以现状为准。

---

## 🎯 何时使用本 Skill

- 新建一个纯 Avalonia UI 插件（演示页、控件展示、工具页）
- 为现有插件添加页面、导航项、菜单项
- 插件注册 DI 服务、设置项、本地化资源
- 排查插件加载/注册问题

---

## 📦 csproj 模板（单一事实来源）

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Library</OutputType>
    <!-- 插件元数据：构建期由 GeneratePluginManifest 目标写入 plugin.json -->
    <PluginId>固定UUID-勿与代码硬编码不一致</PluginId>
    <PluginName>My Plugin</PluginName>
    <PluginAuthor>...</PluginAuthor>
    <PluginDescription>...</PluginDescription>
    <PluginVersion>1.0.0</PluginVersion>
    <MinPluginSdkVersion>2.0.0</MinPluginSdkVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="LYBox.Plugin.Generators" Version="$(PluginSdkVersion)"
      OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <PackageReference Include="LYBox.Plugin.Shared" Version="$(PluginSdkVersion)" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

✅ **PluginId 一致性（已解决）**：元数据由源生成器从 csproj 注入，入口类不再手写 `IPluginMetadata` 属性，消除 csproj 与代码双处硬编码不一致问题（O-1+O-8）。

⚠️ **禁止**：不要在非 Web 插件中创建 `wwwroot/` 目录——Web 资源由 `<PluginKind>Web</PluginKind>` 声明驱动，非 Web 插件不应携带 Web 资源。

---

## 🧩 入口类模式

```csharp
[GenerateMetadata]   // 源生成器从 csproj 注入元数据，生成 IPlugin + IPluginMetadata 实现
public partial class MyPlugin : IPluginMetadata
{
    // 元数据属性（Name/Version/Author/Description/PluginId/MinPluginSdkVersion）全部由生成器从 csproj 注入，无需手写

    // 可选：注册 DI 服务（默认实现已返回 CompletedTask，空实现无需 override）
    public Task InitializeAsync(IServiceCollection services) => Task.CompletedTask;

    // 典型：本地化注册（宿主批量注册时自动扫描 resx ResourceManager，此方法通常无需手写）
    public Task RegisterAsync(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService<ILocalizationService>() is { } loc)
            loc.RegisterResourceManager(Resources.Strings.ResourceManager);
        return Task.CompletedTask;
    }
}
```

要点：
- 不直接实现 `IPlugin`——`[GenerateMetadata]` 源生成器自动补全 `GetViewDefinitions/GetNavigationItems/GetMenuItems`；
- `InitializeAsync`/`RegisterAsync`/`ShutdownAsync` 均有接口默认实现，**空逻辑不要 override**（曾存在 10/12 插件的多余空 override，已清理，O-7）；
- 本地化批量注册由宿主 `PluginLoader` 自动扫描完成（O-6/O-14），插件一般无需手动注册 resx；
- 持有原生/后台资源（如 TdLib 客户端、HTTP 长连接）时**必须** override `ShutdownAsync` 释放——宿主退出时会调用 `PluginLoader.ShutdownAllPluginsAsync`。

---

## 🏷️ 特性驱动注册（源生成器消费）

| 特性 | 标注目标 | 作用 | 生成结果 |
|------|---------|------|---------|
| `[ViewMap(typeof(MyView))]` | ViewModel 类 | VM→View 映射 | `GetViewDefinitions()` 条目，ViewLocator 解析 |
| `[NavigationItem("my-feature")]` | ViewModel 类 | 注册导航 key | `GetNavigationItems()` 条目，`NavigationService.Navigate("my-feature")` 可达 |
| `[Menu(Header, Key, ParentKey)]` | ViewModel 类 | 注册菜单项 | `GetMenuItems()` 条目，菜单树构建 |

```csharp
[NavigationItem("my-feature")]
[Menu("我的页面", "my-feature", parentKey: null, Order = 10)]
[ViewMap(typeof(MyPageView))]
public partial class MyPageViewModel : ViewModelBase { }
```

菜单图标：`[Menu]` 的 `IconName` 命名属性可指定 Fluent 图标资源 key。父菜单图标已从硬编码 `ParentIconMap` 改为继承子菜单图标（O-9），无需改 Shared 库。

---

## 🌐 本地化

1. `Resources/Strings.resx`（默认，建议 zh-CN）+ `Strings.en.resx` 等语言变体；
2. 宿主 `PluginLoader` 批量注册时自动扫描各插件 resx `ResourceManager` 并统一重建缓存（O-6/O-14），插件一般无需在 `RegisterAsync` 手动注册；
3. XAML/代码经 `ILocalizationService` 取串。

---

## ⚙️ 设置注册（参考 Downloader 插件）

`RegisterAsync` 中经 `ISettingsService` 注册 `SettingDefinition`（路径、代理等），参考 `DownloaderPlugin.cs:32-53`。设置值由宿主设置页渲染与持久化，插件不要自建设置 UI。

---

## 🔌 服务注册规范

- DI 注册放 `InitializeAsync(IServiceCollection)`；
- **禁止**注册静态单例（反例：`BTSouPlugin.cs:25` 注册 `BTSouSearchService.Current`）——一律交给容器管理生命周期；
- 解析服务优先构造函数注入；插件代码内静态解析用 `ServiceLocator.TryGetService<T>()`（先 Try 后用，`GetService<T>` 会抛异常）。

---

## 🔄 生命周期约束（强制前提）

| 规则 | 说明 |
|------|------|
| 无热加载/热卸载 | 插件启用/禁用/卸载通过 `plugin.json` 状态字段，**重启生效**；UI 操作需提示用户重启 |
| 状态机 | `NotInstalled → Installed → Loaded → Disabled → PendingUninstall / PendingUpgrade / Error` |
| 启动加载顺序 | Discover（创建 ALC+反射）→ Initialize（DI 注册）→ Register（宿主服务就绪后）→ 导航/菜单注册 |
| 退出 | `App.OnShutdownRequested` 调用各插件 `ShutdownAsync()`；持有原生资源必须实现 |

---

## 🎨 UI 规范（强制）

组件选型与样式**必须**遵守根目录 `AGENTS.md` 的「UI 组件与样式规范」章节：
- 控件优先级：Irihi.Ursa（`u:`）→ Avalonia 内置 → 项目 Fluent 补充样式（`FluentDesignStyles.axaml`）；
- 唯一视觉风格：Fluent Design；禁止 Semi 硬编码色值与 `Avalonia-Fluent-UI` 包；
- 图标只用 `Theme/Icons/` 下的 `FluentIcon{Size}{Variant}{Name}` StreamGeometry 资源，禁止 `Geometry.Parse` 字面量；
- VM 一律 `ObservableObject` + `[ObservableProperty]` + `[RelayCommand]`，绑定走 CompiledBindings（需正确 `x:DataType`）。

---

## ✅ 完成检查清单

- [ ] csproj 元数据齐全（`PluginId/PluginName/PluginVersion` 等），无需手写入口类元数据属性
- [ ] 入口类 `[GenerateMetadata]` + `partial` + 实现 `IPluginMetadata`
- [ ] 无 `wwwroot/` 目录、无 `<PluginKind>Web</PluginKind>`（那是 Web 插件的事）
- [ ] 页面 VM：`[ViewMap]` + `[NavigationItem]` + `[Menu]` 三件套齐全
- [ ] resx 本地化已注册；设置经 `ISettingsService`
- [ ] 无空 override；有后台资源时实现 `ShutdownAsync`
- [ ] `dotnet build` 通过且输出目录生成 `plugin.json`
- [ ] 完整验证：`.\build.ps1 --build=plugin`（需先 `--build=bin` 打 SDK 包）

---

## ❌ 反模式

- 在非 Web 插件里引用 `Avalonia.Controls.WebView` 或 `LYBox.Plugin.Shared.Web` 包
- 手写 `IPlugin.GetViewDefinitions()` 等生成器已接管的方法
- 手写 `IPluginMetadata` 属性（应由源生成器从 csproj 注入）
- 注册静态单例到 DI；在插件里直接操作其他插件的服务/资源
- 硬编码颜色/Geometry；手写 INPC 属性（应 `[ObservableProperty]`）
