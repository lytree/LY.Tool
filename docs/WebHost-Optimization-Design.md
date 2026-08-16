# WebHost 服务器与静态资源服务优化设计方案

> 状态：设计中（仅文档设计，未实施）
> 日期：2026-08-16
> 范围：`WebHostService`、`IWebPlugin` 契约、plugin.json 清单、构建脚本、SDK 包结构
> 允许破坏性更改（Breaking Changes）

---

## 1. 背景与现状

### 1.1 当前架构事实（以代码为准）

| 组件 | 位置 | 现状 |
|------|------|------|
| WebHostService | `src/Plugin/LYBox.Plugin.Shared/Web/WebHostService.cs` | 嵌入式 Kestrel，`http://127.0.0.1:0`（OS 分配端口），懒启动 |
| Web 插件契约 | `src/Plugin/LYBox.Plugin.Shared/Web/IWebPlugin.cs` | `IWebPlugin : IPlugin`，含可写 `PluginBaseDir`、`WwwrootPath`、`EntryPage` |
| 注册方式 | 插件 `RegisterAsync` 中手动调用 `WebHostService.MapPluginRoot(...)` | OPT-IN 软约定 |
| 清单 | `PluginManifest.cs` | 11 个字段，**无任何 Web 分类字段** |
| 构建 | `build/build.cs` `CopyPluginWwwroot` | 仅凭 `wwwroot/` 目录是否存在决定拷贝 |
| 唯一 Web 插件 | `plugins/LYBox.Plugin.WebTemplate` | 实现完整注册链路 |

### 1.2 现有懒加载行为（需要保留的既有优点）

当前 `WebHostService` **已经是懒启动**，并非无条件监听：

- `WebHostService.StartAsync`（`WebHostService.cs:165-168`）：`_pluginRoots.Count == 0` 时直接返回，不启动 Kestrel；
- `App.InitializeWebHost`（`App.axaml.cs:159-189`）：`!webHost.HasRegisteredPlugins` 时记录日志并返回；
- 无 Web 插件的应用完全不占用端口。

本方案在此基础上解决的是**注册契约的结构性问题**，而非"是否懒启动"。

### 1.3 问题清单（本方案要解决的）

| # | 问题 | 证据 | 等级 |
|---|------|------|------|
| P1 | **注册是软约定**：插件实现 `IWebPlugin` 后必须手写 `MapPluginRoot`，忘写则运行期静默降级为占位提示，构建期与加载期均不报错 | `WebTemplatePlugin.cs:23-29`；`WebPluginView.axaml.cs:156-160` | 高 |
| P2 | **清单与构建期无法区分 Web 插件**：plugin.json 无分类字段；`CopyPluginWwwroot` 仅凭目录存在与否判断——非 Web 插件误放 `wwwroot/` 也会被打包，Web 插件漏放 `wwwroot/` 构建期不报错 | `PluginManifest.cs:3-31`；`build.cs:322-336` | 高 |
| P3 | **所有插件背负 Web 依赖**：`LYBox.Plugin.Shared` 单体引用 `Avalonia.Controls.WebView` + `FrameworkReference Microsoft.AspNetCore.App`，11 个非 Web 插件被迫间接引用 ASP.NET Core 框架 | `LYBox.Plugin.Shared.csproj:84` | 高 |
| P4 | **Web 标识需加载程序集才能识别**：宿主通过 `e.Plugin is IWebPlugin`（`PluginLoader.cs:538`）类型判断，清单阶段（创建 ALC 之前）无法知道插件是否为 Web 插件 | `PluginLoader.cs:532-547` | 中 |
| P5 | **宿主职责泄漏到插件**：`IWebPlugin.PluginBaseDir` 是可写属性，由宿主注入；`WwwrootPath`/`EntryPage` 的默认逻辑放在接口上，插件可随意改写导致与清单脱节 | `IWebPlugin.cs:17-29` | 中 |
| P6 | **Disabled 语义不完整**：`MapPluginRoot` 注册只发生在启动序列中，与插件状态机（Disabled/PendingUninstall）无联动约束，注册正确性依赖插件自身判断 | `PluginLoader.cs:152-189` | 中 |
| P7 | **origin 白名单硬编码**：`TryAuthorize` 仅放行自身 BaseUrl，无配置能力（SSE 与静态资源共用同一规则） | `WebHostService.cs:343-367` | 低 |

---

## 2. 设计目标

1. **显式声明优先**：Web 插件的标识在 csproj → plugin.json → 运行时三层一致、单一事实来源（Single Source of Truth）。
2. **声明即注册**：插件作者不再手写 `MapPluginRoot`；实现声明式契约后由宿主统一注册，消除"忘写注册"类错误。
3. **依赖最小化**：非 Web 插件的依赖图中不出现 `Avalonia.Controls.WebView` 与 `Microsoft.AspNetCore.App`。
4. **保持懒启动不变**：无 Web 插件注册时 Kestrel 不启动、不占端口（现有行为为正确设计，固化为契约与测试）。
5. **构建期校验前置**：Web 插件的 wwwroot/EntryPage 缺失在 `dotnet build` 即失败，而非运行期占位。

---

## 3. 目标设计

### 3.1 三层显式声明模型

```text
┌─────────────────────────────────────────────────────────────┐
│ 第 1 层：csproj 声明（作者意图，单一事实来源）                    │
│   <PluginKind>Web</PluginKind>                               │
│   <PluginWwwroot>wwwroot</PluginWwwroot>                     │
│   <PluginEntryPage>index.html</PluginEntryPage>              │
├─────────────────────────────────────────────────────────────┤
│ 第 2 层：plugin.json 清单（构建期生成，加载期消费）               │
│   "kind": "Web",                                             │
│   "web": { "wwwroot": "wwwroot", "entryPage": "index.html" } │
├─────────────────────────────────────────────────────────────┤
│ 第 3 层：运行时契约（源生成器强制，宿主统一注册）                  │
│   [GenerateMetadata(Kind = PluginKind.Web)]                  │
│   → 宿主 PluginLoader 在 Discover 阶段读取清单 kind           │
│   → RegisterAllPluginsAsync 之前统一 MapPluginRoot           │
│   → InitializeWebHost 懒启动（现状保留）                        │
└─────────────────────────────────────────────────────────────┘
```

#### 3.1.1 csproj 层

新增 MSBuild 属性（由 `LYBox.Plugin.Shared.props` 提供默认值）：

| 属性 | 默认值 | 说明 |
|------|--------|------|
| `PluginKind` | `Avalonia` | 插件类别：`Avalonia` \| `Web` |
| `PluginWwwroot` | `wwwroot` | 仅 `PluginKind=Web` 时生效 |
| `PluginEntryPage` | `index.html` | 仅 `PluginKind=Web` 时生效 |

**构建期校验（新增 MSBuild 目标 `ValidatePluginKind`，AfterTargets=`GeneratePluginManifest`）**：

- `PluginKind=Web` 且 `$(PluginWwwroot)` 目录不存在 → `MSBuild Error LYBOX001`；
- `PluginKind=Web` 且入口页文件不存在 → `MSBuild Error LYBOX002`；
- `PluginKind=Avalonia` 且存在 `wwwroot/` 目录 → `MSBuild Warning LYBOX003`（提示该目录不会被拷贝）。

#### 3.1.2 清单层（plugin.json schema v2）

```json
{
  "pluginId": "...",
  "name": "...",
  "version": "...",
  "kind": "Web",
  "web": { "wwwroot": "wwwroot", "entryPage": "index.html" }
}
```

- `kind` 缺省时解析为 `Avalonia`（向后兼容：旧清单无需迁移即可加载）；
- `web` 对象仅 `kind=Web` 时存在；
- `PluginManifest` POCO 新增 `Kind`（默认 `Avalonia`）与 `WebDescriptor` 两个字段。

**收益**：宿主在 `LoadAllPluginManifests` 阶段（创建任何 AssemblyLoadContext 之前）即可统计 Web 插件数量——解决 P4。

#### 3.1.3 运行时契约层（破坏性重设计）

`IWebPlugin` 瘦身为纯只读契约，宿主注入逻辑全部收回：

```csharp
// 目标形态（设计稿，不实施）
public interface IWebPlugin : IPlugin
{
    // 移除：PluginBaseDir 可写属性、WwwrootPath/EntryPage 带默认实现的属性
    // 新增：只读描述符，由源生成器从 csproj 传入的常量实现
    IWebPluginDescriptor Web { get; }
}

public interface IWebPluginDescriptor
{
    string WwwrootPath { get; }   // 宿主拼接安装路径
    string EntryPage { get; }     // 默认 "index.html"
}
```

- `[GenerateMetadata]` 生成器扩展：读取 csproj 的 `PluginKind`/`PluginWwwroot`/`PluginEntryPage`（经 `AnalyzerConfig` 传入或以常量注入生成的 `IPluginMetadata` 属性），自动生成 `Web` 描述符实现；
- `PluginLoader.InjectWebPluginBaseDirs` 更名并重构为 `RegisterWebPlugins`：以**清单**为输入（`kind=Web` 且 `State=Loaded`），由宿主统一调用 `WebHostService.MapPluginRoot`——插件代码中不再出现任何注册调用；
- `WebPluginView` 打开条件校验升级为：清单 `kind=Web` + `WebHostService.IsRegistered` + 会话创建三重校验（任一失败显示带错误码的占位页，而非泛泛提示）。

### 3.2 SDK 拆分（依赖最小化）

`LYBox.Plugin.Shared` 拆为两个包（版本号仍与宿主统一发版，仅包数量 +1）：

```text
LYBox.Plugin.Shared          （核心包，所有插件引用）
├── IPlugin / IPluginMetadata / Attributes/
├── Services/（服务接口）/ Models/
├── ViewLocator / ServiceLocator / ViewModelBase
├── MenuItemTreeBuilder / 转换器 / DataTemplates
└── 依赖：Avalonia、CommunityToolkit.Mvvm、Microsoft.Extensions.DI、EF Core、Ursa、ProDataGrid

LYBox.Plugin.Shared.Web      （Web 包，仅 Web 插件引用）
├── Rpc/（IRpcHost、WebViewIpcHost、Channel、SseEventPusher、ipc.js 资源）
├── Web/（IWebPlugin、WebHostService、WebPluginView、WebViewIpcTransport、
│         WebPluginBindings、SystemCommands、DebugPanelHtml、PluginWebViewDevTools）
├── FrameworkReference: Microsoft.AspNetCore.App
├── PackageReference: Avalonia.Controls.WebView
└── 依赖核心包
```

**共享程序集清单同步调整**（`LYBox.Plugin.Shared.props` / `.targets`）：

- `Microsoft.AspNetCore.*` 相关条目**仅保留在 Web 包**的共享清单与 `ExcludeSharedAssembliesFromCopyLocal` 中；
- 非 Web 插件的 `shared-assemblies.txt` 与发布输出中不再出现 ASP.NET Core 程序集；
- 两处清单（props 与 targets）改由单一源生成（见 `Plugin-SDK-Optimization-Analysis.md` 建议 S-9）。

**宿主侧**：`LYBox.Launcher.Desktop` 同时引用两包；`App.Initialize` 中 `WebHostService` 的注册改为条件化（尝试解析 Web 包程序集，失败则记录"Web SDK 未安装，Web 插件功能不可用"——使宿主本身也可按发行版裁剪 Web 能力）。

### 3.3 注册时序（目标状态）

```text
App.Initialize()
  ├─ 1. PluginLoader 构造（清单加载，此时已知 kind=Web 的插件集合）
  ├─ 2. DiscoverAllPluginAssembliesAsync()        // 创建 ALC、加载程序集
  ├─ 3. InitializeAllPluginsAsync(services)       // 插件 DI 注册
  ├─ 4. services.AddSingleton<WebHostService>()   // 仅当存在 Web SDK
  ├─ 5. BuildServiceProvider / ServiceLocator
  ├─ 6. RegisterWebPlugins()                      // ★ 宿主统一 MapPluginRoot
  │      输入：清单 kind=Web 且 State=Loaded 的插件
  │      动作：拼接 {InstallPath}/{web.wwwroot} → MapPluginRoot
  ├─ 7. RegisterAllPluginsAsync(provider)         // 插件自身的 Register（不再含注册调用）
  ├─ 8. InitializeWebHost()                       // ★ 懒启动（现状保留并固化为契约）
  │      HasRegisteredPlugins == false → 不启动 Kestrel（日志 + 可测试断言）
  └─ 9. RegisterPluginNavigationAndMenus()
```

**时序契约**：`RegisterWebPlugins`（步骤 6）必须先于 `InitializeWebHost`（步骤 8）；`WebHostService.StartAsync` 在 `_pluginRoots.Count == 0` 时必须为 no-op。两条契约纳入单元测试。

### 3.4 WebHostService 本体优化

| 项 | 现状 | 目标 |
|----|------|------|
| `MapPluginRoot` 可见性 | public（插件可调） | 收缩为 `internal`，仅宿主 PluginLoader 可调用（经 `InternalsVisibleTo` 或注入委托） |
| 注册校验 | 路径存在性检查 | 增加与清单 `web.wwwroot` 的一致性校验，不一致时警告日志 |
| 端点挂载 | 静态资源 + SSE + RPC 桥 + 调试面板全量注册 | 保持（端点本身轻量），但 `#if DEBUG` 调试端点维持编译裁剪；`/__rpc`、`/__emit`、`/__channel/close` 三个浏览器模式端点合并为一个 `/__bridge/{pluginId}/{action}` 路由，缩小路由表与攻击面 |
| origin 校验 | 硬编码仅 BaseUrl（P7） | 改为 `IReadOnlyList<Uri> AllowedOrigins`，默认 `[BaseUrl]`；`WebHostService` 构造时可选注入（宿主保留配置入口） |
| MIME 表 | `GuessMimeType` 硬编码 25+ 扩展名 | 改用 `Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider`（AspNetCore.Framework 内置，无新增依赖），仅保留兜底 `application/octet-stream` |
| 生命周期 | `IAsyncDisposable`，宿主退出时释放 | 增加 `EnsureStoppedAsync()` 显式关闭入口，`App.OnShutdownRequested` 优先于 `ServiceProvider.Dispose()` 调用，确保 SSE 连接先于 Kestrel 关闭 |

### 3.5 构建脚本调整（build/build.cs）

| 现状 | 目标 |
|------|------|
| `CopyPluginWwwroot` 凭目录存在与否拷贝 | 读取 csproj 解析出的 `PluginKind`/`PluginWwwroot`（`PluginProjectInfo` 新增字段），`kind=Web` 才拷贝；`kind=Avalonia` 但存在 wwwroot 时构建警告 |
| `EnsurePluginManifest` 兜底生成无 kind 字段 | 兜底模板同步 v2 schema（kind/web） |
| zip 打包对 wwwroot 无校验 | 打包前断言：`kind=Web` 的插件 zip 中必须含 `wwwroot/{entryPage}`，否则构建失败（LYBOX004） |

---

## 4. 破坏性更改清单（BC）

| # | 更改 | 影响面 | 迁移方式 |
|---|------|--------|----------|
| BC-1 | plugin.json schema v2（新增 `kind`、`web` 字段） | 所有插件清单 | 重新构建自动生成；旧清单解析为 `kind=Avalonia`，无需手工迁移 |
| BC-2 | `IWebPlugin` 接口瘦身（移除可写 `PluginBaseDir`、默认实现属性 → 只读 `Web` 描述符） | 现有实现者：`WebTemplatePlugin` | 源生成器自动生成描述符实现；删除插件中手写属性 |
| BC-3 | `WebHostService.MapPluginRoot` 收缩为宿主内部 API | `WebTemplatePlugin.RegisterAsync` 中的手动调用 | 直接删除该调用（宿主统一注册） |
| BC-4 | SDK 拆分：`LYBox.Plugin.Shared.Web` 独立包 | 12 个插件 csproj；`buildTransitive` props/targets；`plugins/nuget.config` 本地源 | 非 Web 插件无需改动（继续引用核心包）；Web 插件新增一行 PackageReference；宿主新增引用 |
| BC-5 | `SharedAssembliesPatterns` 清单缩减（AspNetCore 条目移至 Web 包） | 所有插件的 `shared-assemblies.txt` 重新生成 | 自动（随 BC-4 重新构建） |
| BC-6 | 浏览器模式端点合并为 `/__bridge/{pluginId}/{action}` | `ipc.js` 的 `httpRpc`/`httpEmit`；`lybox-mock` 工具；SDK `env.ts` | 同步更新三处；`lybox-mock` 发新版本 |
| BC-7 | `IPluginMetadata.PluginId` 等元数据改为由源生成器从 csproj 生成（消除双硬编码，见 `Plugin-Implementation-Analysis.md` O-1） | 12 个插件入口类 | 删除手写属性，源生成器接管 |

---

## 5. 实施阶段（仅规划）

| 阶段 | 内容 | 依赖 |
|------|------|------|
| S1 | 清单 v2 + csproj 属性 + `ValidatePluginKind` 构建校验目标（纯增量，向后兼容） | 无 |
| S2 | `PluginLoader.RegisterWebPlugins` 宿主统一注册 + `MapPluginRoot` 收缩（BC-2/BC-3） + WebTemplate 迁移 | S1 |
| S3 | SDK 拆分 `LYBox.Plugin.Shared.Web`（BC-4/BC-5） + 共享清单单源生成 | S2 |
| S4 | WebHostService 本体优化（MIME/origin/bridge 合并/停机时序，BC-6） | S3 |
| S5 | 文档与 skill 同步更新（AGENTS.md、WebView-IPC-Guide.md、两个 plugin skill） | S1–S4 |

每阶段完成标准：单元测试通过（清单解析、构建目标、注册时序、懒启动契约）+ 全插件构建 + WebTemplate 端到端冒烟（无 Web 插件场景断言 Kestrel 未启动）。

---

## 6. 测试策略（设计）

1. **懒启动契约测试**：无任何 `kind=Web` 插件时，`WebHostService.StartAsync` 后 `IsRunning == false`、TCP 端口未分配。
2. **声明即注册测试**：伪造 `kind=Web` 清单 + InstallPath，断言宿主注册后 `IsRegistered(pluginId)` 为 true，无需插件代码参与。
3. **构建校验测试**：`kind=Web` 缺 wwwroot → 构建失败（LYBOX001）；`kind=Avalonia` 带 wwwroot → 警告（LYBOX003）。
4. **依赖裁剪测试**：非 Web 插件发布目录断言不含 `Microsoft.AspNetCore.*.dll`。
5. **安全回归测试**：会话 token 校验、目录穿越防护（`WebHostService.cs:312-322` 现有逻辑）在新路由下保持。

---

## 7. 关联文档

- `docs/Plugin-Implementation-Analysis.md` — 插件实现现状与可优化点（BC-7 的依据）
- `docs/Plugin-SDK-Optimization-Analysis.md` — SDK 依赖与实现简化分析（BC-4 的依据）
- `docs/WebView-IPC-Guide.md` — IPC 机制现行实现
- `.agents/skills/lybox-web-plugin/SKILL.md` / `.agents/skills/lybox-plugin/SKILL.md` — 两类插件的开发 skill（随本方案演进同步更新）
