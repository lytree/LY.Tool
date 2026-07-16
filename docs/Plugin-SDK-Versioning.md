# 插件 SDK 版本契约

本文档描述宿主与插件之间的 SDK 版本契约机制。所有版本真相源在仓库根 [`Directory.Build.props`](../Directory.Build.props)。

---

## 目录

- [两层版本号](#两层版本号)
- [SDK 契约的运行时含义](#sdk-契约的运行时含义)
- [SDK 依赖包清单](#sdk-依赖包清单)
- [manifest 字段优先级](#manifest-字段优先级)
- [运行时校验流程](#运行时校验流程)
- [不兼容时的用户表现](#不兼容时的用户表现)

---

## 两层版本号

| 层级 | MSBuild 属性 | 当前值 | 作用域 | 影响什么 |
|------|--------------|--------|--------|----------|
| **宿主+SDK 同步版本** | `HostVersion` | `2.1.0` | Launcher / UI / Platforms.* / Generators / Shared | 宿主程序集 `AssemblyVersion` / `FileVersion`；SDK NuGet 包版本；`PluginSdkContract.CurrentVersion` 常量 |
| **插件自身版本** | `PluginVersion`（各插件 csproj） | 例如 `1.0.0` | 单个插件 | 该插件的 `plugin.json` version、zip 包名 |

> 注：`PluginSdkVersion` 仍作为别名保留（等价于 `$(HostVersion)`），仅为旧脚本/旧插件 csproj 提供向后兼容，不再单独 bump。

**真相源**：仓库根 [`Directory.Build.props`](../Directory.Build.props)

```xml
<PropertyGroup>
    <HostVersion>2.1.0</HostVersion>
    <PluginSdkVersion>$(HostVersion)</PluginSdkVersion>   <!-- 别名，等价 -->
</PropertyGroup>
```

---

## SDK 契约的运行时含义

`HostVersion` 同时承担 SDK 契约版本语义——它代表 **宿主与插件共享的 API 表面**：

- 升 `HostVersion` Major → 公共 API 破坏性变更，所有插件需重新编译
- 升 Minor → 新增 API，旧插件仍兼容
- 升 Build → 无 API 变更的 bug 修复

宿主与所有插件引用同一份 `LYBox.Plugin.Shared` NuGet 包 → 编译期就能保证双方看到的契约一致；运行时再通过 `PluginSdkContract.CurrentVersion` 常量做一道兜底校验。

---

## SDK 依赖包清单

`LYBox.Plugin.Shared` NuGet 包（即 Plugin SDK）本身引用了以下第三方包。插件通过 `PrivateAssets="all"` 引用 SDK 时，这些依赖会按下面表格中的"加载位置"规则流入插件或由宿主提供。

> **当前 SDK 版本**：`2.1.0`（即 `HostVersion=2.1.0`）
> **真相源**：[`src/Directory.Packages.props`](../src/Directory.Packages.props) + [`src/LYBox.Plugin.Shared/LYBox.Plugin.Shared.csproj`](../src/LYBox.Plugin.Shared/LYBox.Plugin.Shared.csproj) + [`buildTransitive/LYBox.Plugin.Shared.props`](../src/LYBox.Plugin.Shared/buildTransitive/LYBox.Plugin.Shared.props) 中的 `_SharedAssembliesPatterns`

### 1. 共享程序集（由宿主默认 ALC 提供，插件转发引用）

这些包的类型出现在 SDK 公共 API 中，或属于 Avalonia 框架本体，必须由宿主统一加载，避免类型标识冲突。运行时由 [`PluginLoadContext`](../src/LYBox.UrsaWindow/Services/PluginLoadContext.cs) 转发到 `AssemblyLoadContext.Default`。

| 包名 | 当前版本 | 引入版本 | 在 SDK 中的作用 |
|------|---------|---------|----------------|
| `Avalonia` | `12.0.5` | 自 SDK 初始版本 | UI 框架本体，`Control`/`StyledElement` 等基础类型出现在 SDK 公共 API |
| `Avalonia.Skia` | `12.0.5` | 自 SDK 初始版本 | Skia 渲染后端，Avalonia 框架传递依赖 |
| `Irihi.Ursa` | `2.0.*` | 自 SDK 初始版本 | Ursa 控件库，SDK 暴露的控件基类与 `IUriContext` 等公共类型 |
| `Semi.Avalonia` | （Ursa 传递） | 自 SDK 初始版本 | Semi 主题，由 `UrsaSemiTheme` 自动带入 |
| `CommunityToolkit.Mvvm` | `8.4.2` | 自 SDK 初始版本 | `ObservableObject`/`ObservableProperty`/`RelayCommand` —— SDK `ViewModelBase` 与源生成器依赖 |
| `Microsoft.Extensions.DependencyInjection` | `10.0.9` | 自 SDK 初始版本 | `IServiceCollection`/`IServiceProvider` —— `IPlugin.InitializeAsync` 入参类型 |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.9` | 自 SDK 初始版本 | 上者的抽象包，SDK 公共 API 仅依赖抽象 |
| `Microsoft.Extensions.Logging.Abstractions` | `10.0.9` | 自 SDK 初始版本 | `ILogger<T>` 等抽象，插件可从 DI 获取日志器。SDK 仅依赖抽象侧，实现侧由宿主通过 ZLogger 提供 |
| `Microsoft.Extensions.Options` | `10.0.9` | 自 SDK 初始版本 | 选项模式基础类型，传递依赖 |
| `Microsoft.Extensions.Primitives` | `10.0.9` | 自 SDK 初始版本 | `ChangeToken`/`StringValues` 等基础类型，传递依赖 |
| `System.Reactive` | `6.1.0` | 自 SDK 初始版本 | 响应式编程支持，`IObservable<T>` 出现在 SDK 部分 API |
| `Microsoft.Bcl.AsyncInterfaces` | （传递） | 自 SDK 初始版本 | `IAsyncEnumerable<T>` 等异步接口，BCL 传递依赖 |
| `SkiaSharp` / `HarfBuzzSharp` / `MicroCom.Runtime` | （Avalonia 传递） | 自 SDK 初始版本 | Avalonia 原生渲染依赖，通过 `Avalonia.*` 模式匹配共享 |

> **匹配规则**：上述清单由 `LYBox.Plugin.Shared.props` 中的 `_SharedAssembliesPatterns` 属性生成 `shared-assemblies.txt`，运行时 `PluginLoadContext.IsShared(name)` 按"精确名或前缀通配"判断。新增共享程序集必须修改该属性，并升 `HostVersion` Major（破坏性）。

### 2. SDK 传递的私有依赖（包内引用但运行时为插件私有）

这些包被 SDK csproj 引用，但不列入 `shared-assemblies.txt`。SDK 通过 NuGet 传递依赖让所有引用 SDK 的插件都能拿到这些类型，运行时每个插件 ALC 独立加载一份，避免不同插件间版本冲突。

| 包名 | 当前版本 | 引入版本 | 在 SDK 中的作用 | 备注 |
|------|---------|---------|----------------|------|
| `Microsoft.EntityFrameworkCore` | `10.0.9` | SDK 2.1.0 | EF Core 基础抽象包，SDK 公共 API 暴露 `DbContext`/`DbSet` 等抽象类型 | 具体数据库实现（Sqlite 等）由宿主或插件按需引用；宿主引用 Sqlite 用于 `AppDbContext` |
| `ProDataGrid` | `12.0.4` | 自 SDK 初始版本 | 高级 DataGrid 控件，保留供插件编写表格时通过 SDK 传递依赖直接复用 | 插件若需新版本需测试与宿主样式兼容性 |

> **历史变更**：SDK 2.1.0 之前曾引用 `Microsoft.EntityFrameworkCore.Sqlite`（SQLite 实现）与 `Microsoft.Extensions.Logging`（日志实现），自 SDK 2.1.0 起改为仅依赖基础抽象包（EF Core 基础 + Logging 抽象），实现侧由宿主统一提供，避免插件 ALC 加载冗余实现程序集。

### 3. 宿主侧依赖（不在 SDK 中，插件可通过 DI 间接使用）

这些包由宿主 `LYBox.UrsaWindow` 直接引用，不在 `LYBox.Plugin.Shared` NuGet 包内。插件通过 DI 容器获取相关服务时间接使用，**不可直接 `PackageReference` 同名包为公共 API 类型**，否则会引发 ALC 类型标识冲突。

| 包名 | 当前版本 | 引入版本 | 宿主中作用 | 插件访问方式 |
|------|---------|---------|------------|------------|
| `Microsoft.EntityFrameworkCore.Sqlite` | `10.0.9` | 自 SDK 初始版本 | SQLite 数据库实现，宿主 `AppDbContext` 使用 | 插件如需自身持久化可直接 `PackageReference` 同版本（插件私有），或通过宿主 DI 间接使用 |
| `ZLogger` | `2.5.10` | 自 SDK 初始版本 | 结构化日志（UTF-8 + File），作为 `ILogger<T>` 实现侧 | 通过 `ILogger<T>` 抽象间接使用，插件无需直接引用 |
| `Microsoft.Extensions.Localization` | `10.0.9` | 自 SDK 初始版本 | `IStringLocalizer` 等本地化抽象 | 通过 `ILocalizationService`（SDK 抽象）间接使用 |
| `AvaloniaUI.DiagnosticsSupport` | `2.2.3` | 自 SDK 初始版本 | Avalonia DevTools，开发期调试 | 仅 Debug 配置可用，插件不直接接触 |

### 4. 插件私有依赖示例（既不在 SDK 也不在宿主）

以下包既不在 SDK 也不在宿主中，由具体插件自行 `PackageReference` 引用，运行时为该插件 ALC 私有。这类依赖不影响宿主与其他插件，可自由升级版本（仅受兼容性约束）。

| 包名 | 当前版本 | 使用插件 | 用途 |
|------|---------|---------|------|
| `ScottPlot` | `5.1.59` | `LYBox.Plugin.ScottPlot` | 图表绘制控件 |
| `CliWrap` | `3.10.2` | `LYBox.Plugin.Downloader` | 调用外部进程（FFmpeg 等） |
| `TDLib` / `TDLib.Api` / `tdlib.native` | `1.8.*` | `LYBox.Plugin.TDLSharp` | Telegram TDLib 绑定 |

### 关于"引入版本"列的说明

> 上表中"引入版本"标注为"自 SDK 初始版本"的依据：本仓库当前 `HostVersion=2.1.0`，无更早的 SDK 版本记录；从 [`src/Directory.Packages.props`](../src/Directory.Packages.props) 与 SDK csproj 的静态分析可以确认这些包在当前 2.1.0 版本中已存在。
>
> **若需精确追溯某个包在哪个 SDK 版本首次引入**，请使用 git 历史：
>
> ```powershell
> # 查看某个包版本被引入的提交
> git log -p -- src/Directory.Packages.props | Select-String "<AvaloniaVersion>"
>
> # 查看 SDK csproj 引用列表的变更
> git log -p -- src/LYBox.Plugin.Shared/LYBox.Plugin.Shared.csproj
> ```
>
> 当未来新增依赖包时，请在本表中显式标注"引入版本 = X.Y.Z"，并升 `HostVersion` 的 Minor（新增 API，向后兼容）或 Major（破坏性变更）。

---

## manifest 字段优先级

运行时校验读取的是 `plugin.json` 中的 `minPluginSdkVersion`，不是接口成员：

```
插件 .csproj <MinPluginSdkVersion>
    ↓ GeneratePluginManifest target（LYBox.Plugin.Shared.targets）
plugin.json: "minPluginSdkVersion": "..."
    ↓ PluginLoader 读取
PluginInfo.MinPluginSdkVersion
    ↓ IsPluginSdkCompatible(...)
通过 / 拒绝（fail-closed：解析失败也拒绝）
```

`build.cs` 的 `EnsurePluginManifest` fallback 也会写入此字段，确保手动 publish 场景仍含版本信息。

---

## 运行时校验流程

```
读 plugin.json → MinPluginSdkVersion
    ↓
IsPluginSdkCompatible(MinPluginSdkVersion, PluginSdkContract.CurrentVersion)
    ↓
通过 → 继续加载流程
不通过 → 标记 PluginState.Error，写入错误信息，拒绝加载
```

**SemVer 比对规则**（[`PluginLoader.IsPluginSdkCompatible`](../src/LYBox.UrsaWindow/Services/PluginLoader.cs)）：

- `null` / 空 → 通过（无约束）
- 解析失败 → **拒绝**（fail-closed，避免误判不兼容插件）
- 仅比较 `Major.Minor.Build` 三段，预发布标签（`-beta` 等）忽略
- `Major` 不等：`current > required` 才通过
- `Minor` 不等：`current > required` 才通过
- `Build`：`current >= required` 才通过

---

## 不兼容时的用户表现

插件出现在列表中但状态为 `Error`，错误信息形如：

```
Plugin requires Plugin SDK >= 1.4.0, but host provides 1.3.0.
Update the host application or contact the plugin author.
```

错误状态会写回 `plugin.json` 的 `state` 字段，UI 据此显示。用户解决方案：

1. **升级宿主** 到提供 `>= 1.4.0` SDK 的版本
2. 或 **联系插件作者** 提供与当前宿主兼容的版本
