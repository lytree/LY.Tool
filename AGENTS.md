# LYBox — AGENTS.md

OpenCode 智能体在本仓库工作时的精简指南。

## 构建与运行

- **构建系统**：Cake Frosting（`build/build.cs` — .NET 10 文件化应用，Cake 6.1.0）。通过 `.\build.ps1`（Windows）或 `./build.sh`（Linux/macOS）调用。
  ```
  .\build.ps1 --build=all                    # 默认：bin（启动器 + NuGet）+ plugin
  .\build.ps1 --build=bin                    # 构建启动器 + 打包 SDK NuGet 包（host 与 SDK 同版本号，统一发版）
  .\build.ps1 --build=plugin                 # 构建并打包所有插件为 zip
  .\build.ps1 --configuration=Debug          # 覆盖配置（默认：Release）
  .\build.ps1 --package-version=1.2.3        # 设置版本（默认：1.0.0）
  .\build.ps1 --runtime-identifier=win-x64   # 设置启动器发布的 RID
  .\build.ps1 --self-contained=true          # 启动器自包含发布
  .\build.ps1 --nuget-api-key=<KEY>          # 推送包到 nuget.org
  ```
- **构建顺序很重要**：`--build=bin` 必须先于 `--build=plugin` 运行（或直接使用 `--build=all`），因为 `--build=bin` 会打包 SDK NuGet 包，而插件依赖本地构建的 `LYBox.Plugin.Generators` + `LYBox.Plugin.Shared` NuGet 包。
- **直接 `dotnet build`** 可用于单个项目，但若未预先构建本地 NuGet 包，插件可能还原失败（使用 `--build=bin` 或确保 `bin/nuget/` 下有 `.nupkg` 文件）。`--build=nuget` 保留为 `--build=bin` 的兼容别名。
- **运行启动器**：`dotnet run --project src/launcher/LYBox.Launcher.Desktop`
- **VS Code 调试**：使用 "Debug Plugin - {Name}" 启动配置 — 每个配置将 `AVALONIA_EXTRA_PLUGINS_PATH` 指向插件的 `bin/Debug/net10.0` 输出目录，用于开发期实时加载。
- **无测试**，无 CI 工作流，未配置 linter/formatter。

## 架构

### 两个解决方案
| 解决方案 | 内容 |
|----------|----------|
| `Core.slnx` | 宿主：Generators、Shared、UI、Launcher、Platforms.Abstractions |
| `Plugins.slnx` | Generators、Shared、所有 `plugins/*` 项目（10 个插件） |

### 项目分层（src/）
```
LYBox.Plugin.Generators/        Roslyn 增量源生成器（netstandard2.1，IsRoslynComponent）
LYBox.Plugin.Shared/            共享契约：IPlugin、IPluginMetadata、ViewLocator、ServiceLocator、特性、控件
LYBox.Platforms.Abstractions/   跨平台抽象基类（仅空 README）
LYBox.UrsaWindow/                       宿主应用：ViewModels、Views、Services（EF Core、导航、菜单、本地化、ZLogger）
LYBox.Launcher.Desktop/         桌面入口（Program.cs → App.axaml.cs）。设置 AvaloniaUseCompiledBindingsByDefault=true。
```

### 平台特定项目
`src/platforms/` 包含：
- `LYBox.Platforms.Windows` — `net10.0-windows10.0.19041.0`
- `LYBox.Platforms.MacOs` — `net10.0-macos15.0`
- `LYBox.Platforms.Linux` — `net10.0`

### 插件项目（plugins/）
每个插件是 `net10.0` 类库，引用 `LYBox.Plugin.Generators`（analyzer，`OutputItemType="Analyzer"`，`ReferenceOutputAssembly="false"`）和 `LYBox.Plugin.Shared`（`PrivateAssets="all"`）。插件元数据通过 MSBuild 属性声明：
```xml
<PluginId>UUID</PluginId>
<PluginName>...</PluginName>
<PluginAuthor>...</PluginAuthor>
<PluginDescription>...</PluginDescription>
<PluginVersion>1.0.0</PluginVersion>  <!-- 可选，缺省回退到 <Version> -->
```

10 个插件：ButtonsInputs、DateTime、DialogFeedbacks、Downloader、LayoutDisplay、NavigationMenus、ProDataGrid、ScottPlot、TDLSharp、Template。

### 应用启动流程
```
Program.cs → App.Initialize()
  1. 通过 ServiceCollectionExtensions.AddAvaloniaServices() 构建 DI 容器
  2. ServiceLocator.Initialize(provider) — 插件代码使用的静态网关
  3. InitializeDatabase() — 通过 EF Core 初始化 SQLite（AppDbContext）
  4. InitializeLocalization() — 恢复已保存的语言设置
  5. LoadPluginsAsync() — 发现、加载并注册所有插件
  6. OnFrameworkInitializationCompleted() → 显示启动闪屏，然后显示 MainWindow
```

### 插件加载与程序集排除
- 每个插件在独立的、可收集的 `AssemblyLoadContext` 中加载
- 框架/共享程序集转发到默认上下文（排除清单见 `LYBox.Plugin.Shared.props`/`.targets`）
- 插件通过 `GeneratePluginManifest` 目标自动生成 `plugin.json` 清单（来自 `LYBox.Plugin.Shared.targets`）
- 发现：扫描 `{AppBaseDir}/plugins/` 和 `AVALONIA_EXTRA_PLUGINS_PATH` 环境变量
- 构建输出：`bin/plugins/{Name}/publish/`（发布目录）+ `bin/plugins/zip/{Name}-{Version}.zip`（剥离 .pdb、.xml、.deps.json、.runtimeconfig.json）

## 插件系统前提约束（强制）

**当前项目不支持插件热加载与热卸载。** 所有插件在应用启动时一次性加载，状态变更（启用/禁用/标记卸载）需重启应用生效。

基于此前提，开发时遵循以下规则：

| 规则 | 说明 |
|------|------|
| **无运行时插件增删** | 插件安装/卸载/启用/禁用均通过修改 `plugin.json` 状态实现，下次启动时生效。UI 中相关操作需提示用户重启。 |
| **无需处理 ALC 卸载清理** | `PluginLoadContext` 虽标记 `isCollectible=true`，但运行时不调用 `Unload()`。`ViewLocator._viewRegistry`、`LocalizationService._resourceManagers`、`MenuConfigurationService._menuItemsMap` 等静态/长生命周期字典无需在运行时清理。 |
| **应用退出需优雅关闭** | `App.OnShutdownRequested` 中应调用 `IPlugin.ShutdownAsync()` 并 `ServiceProvider.Dispose()`，确保插件持有的原生资源（如 TdLib 客户端）正确释放。 |
| **插件安装冲突处理** | 覆盖安装时若旧插件 ALC 仍持有 DLL 文件锁，需提示用户重启后再安装，或先关闭应用再安装。 |
| **DisablePlugin/EnablePlugin 语义** | 仅修改状态字段并持久化到 manifest，不触发 ALC 卸载/重载。下次启动时按新状态决定是否加载。 |

## 关键模式（请勿破坏）

| 模式 | 要点 |
|---------|-------------|
| **ServiceLocator** | 插件使用的静态 `IServiceProvider` 包装器。在 `App.Initialize()` 中初始化一次。调用 `GetService<T>()` 前先用 `TryGetService<T>()` 检查。 |
| **ViewLocator** | 全局 `IDataTemplate`，使用 `ConditionalWeakTable` 缓存（VM→View 循环无泄漏）。在 XAML 中注册 — `ContentControl.Content="{Binding Content}"` 自动解析。 |
| **导航** | 基于 key 的 `NavigationService` + `WeakReferenceMessenger` 发布/订阅（"JumpTo" 消息）。插件在 `IPlugin.GetNavigationItems()` 中注册导航项。 |
| **菜单层级** | 扁平菜单项 + 可选 `parentKey`。`MenuItemTreeBuilder.BuildTree()` 解析为树。`MenuConfigurationService` 管理增删。 |
| **源生成器** | 在实现 `IPluginMetadata` 的类上标注 `[GenerateMetadata]` → 自动生成 `IPlugin` 实现。扫描伴生类上的 `[ViewMap]`、`[NavigationItem]`、`[Menu]` 特性。 |
| **本地化** | `ILocalizationService` 堆叠 `.resx` `ResourceManager` 实例。插件在 `Initialize()` 中注册自己的 ResourceManager。 |
| **插件生命周期** | `NotInstalled → Installed → Loaded → Disabled → PendingUninstall`。状态变更触发事件通知 UI。 |

## UI 组件与样式规范（强制）

所有 Host UI 与插件 UI 必须遵守以下选型与风格规则。违反规则的 UI 代码视为需重构。

### 1. 组件选型优先级（从高到低）

| 优先级 | 来源 | 用法示例 | 适用场景 |
|--------|------|---------|---------|
| 1 | **Irihi.Ursa**（`u:` 命名空间） | `<u:Button />`、`<u:Banner />`、`<u:NavMenu />`、`<u:Form />`、`<u:NumericUpDown />`、`<u:TagInput />`、`<u:IPv4Box />`、`<u:TimeBox />`、`<u:Avatar />`、`<u:Card />`、`<u:Badge />`、`<u:Loading />`、`<u:Breadcrumb />`、`<u:Dialog />`、`<u:Drawer />` | 默认首选。所有通用控件优先用 Ursa。 |
| 2 | **Avalonia 内置控件**（无 `u:` 前缀） | `<Button />`、`<TextBox />`、`<CheckBox />`、`<ComboBox />`、`<ListBox />`、`<TreeView />`、`<TabControl />`、`<ProgressBar />`、`<Slider />`、`<DatePicker />`、`<DataGrid />` | Ursa 未覆盖或场景不适合 Ursa 时使用。DataGrid 已应用 `<datagrid:DataGridFluentTheme />`。 |
| 3 | **项目自定义 Fluent 补充样式**（`src/LYBox.UrsaWindow/Theme/FluentDesign/FluentDesignStyles.axaml`） | `Button.FluentSettingsCard`、`Border.FluentInfoBadge`、`ProgressBar.circular.FluentProgressRing`、`Button.FluentBreadcrumbItem`、`Border.FluentContentDialogSurface` | Ursa 未提供的 WinUI 风格控件。详见下表。 |
| 4 | **CommunityToolkit.Mvvm** | `ObservableObject`、`[ObservableProperty]`、`[RelayCommand]` | ViewModel 基础设施（与组件选型并列，但所有 VM 必须用此库）。 |

**禁止**：引入 `Avalonia-Fluent-UI`（`AvaloniaFluentUI`）NuGet 包或项目引用。该库与 Irihi.Ursa 大量功能重叠且未发布到 NuGet。需要 WinUI 风格控件时，使用上述第 3 级的项目内补充样式。

### 2. 自定义 Fluent 补充样式速查表

所有补充样式位于 `src/LYBox.UrsaWindow/Theme/FluentDesign/FluentDesignStyles.axaml`，通过 `UrsaSemiTheme` 自动加载，无需手动 `<StyleInclude>`。

| 类名 | 控件类型 | 替代的 WinUI 控件 | 用途 |
|------|---------|------------------|------|
| `FluentSettingsCard` | `Border` 或 `Button` | `SettingsExpander` / `SettingCard` | 设置页条目：左图标 + 标题 + 描述 + 右内容 |
| `FluentSettingsCardTitle` / `FluentSettingsCardDescription` / `FluentSettingsCardIconHost` | `TextBlock` / `Border` | — | SettingsCard 内部子元素样式 |
| `FluentInfoBadge` (+ `.FluentInfoBadgeCritical/Warning/Informational/Success`) | `Border` | `InfoBadge` | 数值或状态徽章 |
| `FluentInfoBadgeText` | `TextBlock` | — | InfoBadge 内数字 |
| `FluentInfoBadgeDot` | `Ellipse` | `InfoBadge` (dot) | 点状徽章 |
| `FluentProgressRing` (+ `.Small` / `.Large`) | `ProgressBar` (Classes=`circular`) | `ProgressRing` | 圆环进度（确定性或 `IsIndeterminate="True"`） |
| `FluentBreadcrumbItem` | `Button` | `BreadcrumbBar` 项 | 面包屑导航可点击项 |
| `FluentBreadcrumbCurrent` / `FluentBreadcrumbSeparator` | `TextBlock` | — | 当前节点 / 分隔符 |
| `FluentContentDialogSurface` / `FluentContentDialogTitle` / `FluentContentDialogBody` / `FluentContentDialogButtonRow` | `Border` / `TextBlock` / `StackPanel` | `ContentDialog` | 模态对话框外观（控件仍走 Ursa `Dialog` API） |
| `FluentNumeric` | `u:NumericUpDown` | `NumberBox` | Ursa NumericUpDown 的 Fluent 边框微调 |
| `FluentTagInput` | `u:TagInput` | — | Ursa TagInput 的 Fluent 边框微调 |

**示例**：
```xml
<!-- SettingsCard -->
<Border Classes="FluentSettingsCard">
    <Grid ColumnDefinitions="Auto,*,Auto">
        <Border Classes="FluentSettingsCardIconHost" Grid.Column="0">
            <Image Source="{DynamicResource FluentIconSettings}" Width="16" Height="16" />
        </Border>
        <StackPanel Grid.Column="1" Margin="12,0" VerticalAlignment="Center">
            <TextBlock Classes="FluentSettingsCardTitle" Text="主题" />
            <TextBlock Classes="FluentSettingsCardDescription" Text="选择浅色或深色外观" />
        </StackPanel>
        <u:ToggleSwitch Grid.Column="2" IsChecked="{Binding EnableDarkMode}" />
    </Grid>
</Border>

<!-- InfoBadge -->
<Border Classes="FluentInfoBadge FluentInfoBadgeCritical" VerticalAlignment="Top">
    <TextBlock Classes="FluentInfoBadgeText" Text="3" />
</Border>

<!-- ProgressRing -->
<ProgressBar Classes="circular FluentProgressRing" IsIndeterminate="True" />

<!-- Breadcrumb -->
<ItemsControl ItemsSource="{Binding BreadcrumbSegments}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate><StackPanel Orientation="Horizontal" /></ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <Button Classes="FluentBreadcrumbItem" Content="{Binding Title}" Command="{Binding NavigateCommand}" />
                <TextBlock Classes="FluentBreadcrumbSeparator" Text="/" />
            </StackPanel>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

### 3. 样式风格约束（限定 Fluent-UI 风格）

- **唯一允许的视觉风格**：Fluent Design System（WinUI 3 风格）。
- **禁止**直接使用 Semi 风格的硬编码色值。Semi 资源键（如 `SemiColorText0`、`SemiColorText1`、`SemiColorText2`、`SemiColorDanger`、`SemiColorWarning`、`SemiColorSuccess`、`SemiColorPrimary`）**仅作为动态资源引用**，由 `UrsaSemiTheme` 的 ThemeDictionary 自动映射到 Fluent 配色，不允许在 XAML 中写死颜色字面量（如 `#FF0078D4`）。
- **颜色资源层级**：
  1. Fluent 语义资源（首选）：`FluentAccentBrush`、`FluentAccentPointeroverBrush`、`FluentAccentPressedBrush`、`FluentCardBackgroundBrush`、`FluentCardStrokeBrush`、`FluentSubtleBrush`、`FluentSubtleHoverBrush`、`FluentSubtlePressedBrush`
  2. Semi 语义资源（次选，由 UrsaSemiTheme 自动适配到 Fluent 配色）：`SemiColorText0/1/2`、`SemiColorPrimary`、`SemiColorDanger`、`SemiColorWarning`、`SemiColorSuccess`、`SemiColorInfo`
  3. 字面量颜色（仅用于阴影 `BoxShadow`、`Opacity` mask 等无法用语义资源表达的场景）：使用 `#XXRRGGBB` 格式，且必须注释说明原因
- **圆角规范**：卡片 8px、徽章/小按钮 4px、点状元素圆形（`CornerRadius="0"` + `CornerRadius` 全值 = 宽/2）。
- **间距规范**：内边距遵循 12/16/24 三档；元素间用 `Spacing` 而非 `Margin`。
- **动画规范**：颜色/画刷过渡统一用 `BrushTransition`，时长 `0:0:0.15`；阴影过渡用 `BoxShadowsTransition`。复杂动画引用 `Theme/Animations/` 下的 `DefaultSizeAnimations`、`NavMenuSizeAnimations`、`SemiPopupAnimations`。
- **主题入口**：所有样式通过 `src/LYBox.UrsaWindow/Theme/UrsaSemiTheme.axaml` 注册，应用入口 `App.axaml` 仅引用 `<fluent:FluentTheme />` + `<theme:UrsaSemiTheme />` + `<sizeanimations:SemiPopupAnimations />` + `<datagrid:DataGridFluentTheme />`，**不要**在 `App.axaml` 中追加额外 `<StyleInclude>`。

### 4. 图标使用规则（优先 Fluent-UI icon）

- **首选图标集**：Fluent Icons（Microsoft Fluent UI System Icons）。资源位于 `src/LYBox.UrsaWindow/Theme/Icons/Fluent/`，按 `Regular/Filled` × `16/20/24/28/32/48` 切分。
- **图标资源键命名规范**：`FluentIcon{Size}{Variant}{Name}`，例如：
  - `FluentIcon24RegularSettings`
  - `FluentIcon20FilledWarning`
  - `FluentIcon16RegularChevronDown`
- **图标引用方式**（按控件类型选择）：
  1. **`PathIcon` / `Image`**（首选，矢量）：
     ```xml
     <PathIcon Data="{DynamicResource FluentIcon24RegularSettings}" Width="20" Height="20" />
     <!-- 或 -->
     <Image Source="{DynamicResource FluentIcon24RegularSettings}" Width="20" Height="20" />
     ```
  2. **`Button.Content`**（按钮内图标）：
     ```xml
     <Button Classes="FluentSettingsCard">
         <PathIcon Data="{DynamicResource FluentIcon24RegularSettings}" />
     </Button>
     ```
  3. **Ursa `IconButton`**（推荐用于纯图标按钮）：
     ```xml
     <u:IconButton Icon="{DynamicResource FluentIcon24RegularSettings}" />
     ```
- **次选图标集**：项目自定义 `Semi` 风格图标（`src/LYBox.UrsaWindow/Theme/Icons/_index.axaml` 中以 `SemiIcon` 开头的资源键，如 `SemiIconChevronDown`）。仅当 Fluent Icons 中找不到对应图标时使用，且需在代码注释中说明原因。
- **禁止**：硬编码 `Geometry.Parse("...")` 字面量路径。所有路径必须以 `StreamGeometry` 资源形式定义在 `Theme/Icons/` 下。
- **新增 Fluent 图标流程**：
  1. 从 [Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons) 获取 SVG path
  2. 转换为 `<StreamGeometry x:Key="FluentIcon{Size}{Variant}{Name}">path data</StreamGeometry>`
  3. 追加到对应尺寸的 `Theme/Icons/Fluent/{Variant}{Size}.axaml`
  4. 在 XAML 中以 `{DynamicResource FluentIcon...}` 引用

### 5. ViewModel 与数据绑定

- **ViewModel 基类**：所有 VM 继承 `CommunityToolkit.Mvvm.ComponentModel.ObservableObject` 或项目 `ViewModelBase`。
- **属性**：用 `[ObservableProperty]` 自动生成 INPC。**禁止**手写 `private T _field; public T Foo { get => _field; set => SetProperty(ref _field, value); }`。
- **命令**：用 `[RelayCommand]` 自动生成 `ICommand`。**禁止**手写 `RelayCommand`/`DelegateCommand` 实例。
- **CompiledBindings**：`AvaloniaUseCompiledBindingsByDefault=true`（已全局开启）。所有 `Binding` 必须有正确的 `x:DataType`，避免运行时反射开销。
- **MVVM Toolkit 源生成器**：partial VM 类必须标注 `[INotifyPropertyChanged]` 或继承 `ObservableObject`，否则 `[ObservableProperty]` 不会生成。

## 包与框架版本

所有版本以 MSBuild 属性形式集中管理于 `src/Directory.Packages.props`：
- Avalonia: `12.0.3` (`$(AvaloniaVersion)`)
- Irihi.Ursa: `2.0.*` (`$(IrihiUrsaVersion)`)
- CommunityToolkit.Mvvm: `8.4.2` (`$(CommunityToolkit)`)
- EF Core: `10.0.8` (`$(EfCoreVersion)`)
- Microsoft.Extensions.DI: `10.0.8` (`$(MicrosoftExtensionsDI)`)
- Microsoft.Extensions.Localization: `10.0.8`
- AvaloniaUI.DiagnosticsSupport: `2.2.1`
- ProDataGrid: `12.0.0`
- ScottPlot: `5.1.58`
- ZLogger: `2.1.0`
- 插件 NuGet 包：`LYBox.Plugin.Generators` + `LYBox.Plugin.Shared`，版本 `1.0.0`，本地构建到 `bin/nuget/`

## NuGet 配置

- **根 `nuget.config`**：将 `globalPackagesFolder` 设置为 `<repo>/packages`（本地缓存，在 `.gitignore` 中以 `packages/nuget/` 例外跟踪）
- **`plugins/nuget.config`**：继承根配置，新增 `LYBoxPluginLocal` 源指向 `<repo>/bin/nuget` — 插件通过此源解析本地构建的 `LYBox.Plugin.Generators` 和 `LYBox.Plugin.Shared` 包

## 平台目标

`src/Environment.props` 管理平台特定的 TFM：
- Windows: `net10.0-windows10.0.19041.0` + 定义 `Platforms_Windows`
- macOS: `net10.0-macos15.0` + 定义 `Platforms_MacOs` + `SupportedOSPlatformVersion=10.15`
- Linux: `net10.0`（无平台后缀）+ 定义 `Platforms_Linux`
- 开发模式通过 `[System.OperatingSystem]::IsWindows()` 等自动检测 OS
- CI 使用 `PublishBuilding=true` + `PublishPlatform=windows|linux|macos`
- Release + Windows → `OutputType=WinExe`

## 已安装的 Skills（本地）

`.agents/skills/` 中有三个 Avalonia/Zafiro skill（来自 `sickn33/antigravity-awesome-skills`）：
- `avalonia-layout-zafiro` — XAML 布局约定
- `avalonia-viewmodels-zafiro` — ViewModel/Wizard 模式
- `avalonia-zafiro-development` — 强制约定与规则

这些 skill 处于激活状态，当其模式适用时应予使用。

## 插件 .csproj 模板（用于新插件）

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Library</OutputType>
    <PluginId>...</PluginId>
    <PluginName>...</PluginName>
    <PluginAuthor>...</PluginAuthor>
    <PluginDescription>...</PluginDescription>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="LYBox.Plugin.Generators" Version="1.0.0"
      OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <PackageReference Include="LYBox.Plugin.Shared" Version="1.0.0" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

## 注意事项

- `.slnx` 格式（非 `.sln`）— .NET 10 XML 解决方案格式
- 构建脚本（`build/build.cs`）通过扫描 `plugins/` 下所有 `*.csproj` 发现插件 — `PluginId` 等从 .csproj XML 读取
- `Core.slnx` 和 `Plugins.slnx` 共享 `src/LYBox.Plugin.Generators` 和 `src/LYBox.Plugin.Shared`
- 插件 NuGet 包必须在还原插件前本地构建。先用 `.\build.ps1 --build=bin` 构建；包输出到 `bin/nuget/`。`plugins/nuget.config` 将此目录添加为本地源。
- `AvaloniaUseCompiledBindingsByDefault` 在启动器项目中设为 `true` — 新插件应遵循此约定
- `src/` 下的 `Directory.Build.props` 导入 `Environment.props` 并设置默认 `TargetFramework=net10.0`（按平台覆盖）
- Generators 项目目标为 `netstandard2.1`（Roslyn 源生成器约束），其余项目均为 `net10.0`
- 仓库中无 `opencode.json` 或 `CLAUDE.md` — 本 `AGENTS.md` 是唯一的指令文件
