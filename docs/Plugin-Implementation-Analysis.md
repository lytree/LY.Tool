# 插件实现现状分析与可优化内容

> 状态：分析完成（仅文档，未实施）
> 日期：2026-08-16
> 范围：`plugins/` 全部 12 个插件、`src/Plugin/`（契约与源生成器）、`src/Layout/`（PluginLoader 及注册服务）
> 关联：`docs/WebHost-Optimization-Design.md`（Web 方向）、`docs/Plugin-SDK-Optimization-Analysis.md`（SDK 方向）

---

## 1. 插件清单与分类

当前实际存在 12 个插件（**注意**：AGENTS.md 声称的 `BlazorApp` 源码已删除，仅剩 `obj/`、`bin/` 构建产物，不在统计内）：

| 分类 | 插件 | 特征 |
|------|------|------|
| 纯 UI 演示（8） | ButtonsInputs、DateTime、DialogFeedbacks、LayoutDisplay、NavigationMenus、ProDataGrid、ScottPlot、Template | `InitializeAsync`/`RegisterAsync` 仅做本地化注册，无 DI 服务、无后台逻辑 |
| 服务/后台型（3） | BTSou（搜索服务）、TDLSharp（TdLib 客户端管理）、Downloader（7 项外部工具路径设置） | 注册 DI 服务或设置 |
| Web 插件（1） | WebTemplate | 唯一实现 `IWebPlugin`，含 wwwroot + RPC 命令 |

### 12/12 插件的共性（样板代码统计）

| 共性 | 占比 |
|------|------|
| 入口类实现 `IPluginMetadata` + `[GenerateMetadata]` + `partial` | 12/12 |
| csproj 与代码**双重硬编码** PluginId/Name/Version/Author/Description | 12/12 |
| `Dependencies => []` 恒为空 | 12/12 |
| 不实现 `ShutdownAsync`（用接口默认） | 12/12 |
| 不实现 `GetIconResources`（用接口默认） | 12/12 |
| 本地化注册 5 行样板（`RegisterResourceManager` 模式） | 11/12（仅 WebTemplate 例外） |
| 空 `InitializeAsync` override（`=> Task.CompletedTask`） | 10/12 |

---

## 2. 可优化点清单

优先级定义：
- **P0 正确性**——已在运行时造成或即将造成真实缺陷；
- **P1 结构**——样板冗余、职责错位、可维护性风险；
- **P2 清理**——死代码、过时约定。

### P0 — 正确性问题

#### O-1. PluginId 双重硬编码且不一致（12 插件中至少 8 个受影响）

**问题**：csproj `<PluginId>` 与入口类 `IPluginMetadata.PluginId` 是两套独立数据源，部分插件两者**完全不同**：

- `plugins/LYBox.Plugin.Template/LYBox.Plugin.Template.csproj:8` → `TEMPLATE-PLUGIN-0000-0000-000000000001`
- `plugins/LYBox.Plugin.Template/TemplatePlugin.cs:17` → `b5eab285-8673-4991-a45a-b43bee2cb840`

**影响**：`PluginLoader.ManifestToPluginInfo`（`PluginLoader.cs:1124`）以清单为准；插件运行时上报的 `PluginId`（代码硬编码）与清单脱钩，`GetLoadedPlugin` 等按 ID 查找的路径失效。受影响插件：Template、ButtonsInputs、BTSou、ScottPlot、ProDataGrid、WebTemplate、TDLSharp、Downloader。

**建议**：`MetadataGenerator` 从 csproj 生成 `IPluginMetadata` 全部属性（经 MSBuild 注入常量），入口类删除手写元数据属性。对应优化设计文档 BC-7。

#### O-2. BTSou 静态单例注入 DI（破坏容器生命周期）

**证据**：`BTSouPlugin.cs:25` — `services.AddSingleton(Services.BTSouSearchService.Current);`。`Current` 为懒初始化静态单例，绕过 DI 生命周期管理，无法替换/测试/释放。

**建议**：改为 `services.AddSingleton<BTSouSearchService>()`，类内移除静态 `Current`。

#### O-3. NavigationService 插件卸载缓存失效 Bug

**证据**：`NavigationService.cs:51-54` — `OnPluginUnloaded` 调用 `InvalidateCache(pluginInfo.PluginId)`，但 `InvalidateCache(string key)`（`NavigationService.cs:110-119`）按**导航 key** 匹配——永远命中失败。虽然当前"无热卸载"前提下事件不触发，但这是留给未来升级的陷阱。

**建议**：`PluginUnloaded` 事件载荷增加导航 key 集合，或 `InvalidateCache` 改为接收 `PluginId` 并遍历工厂表反查。

#### O-4. TDLSharp 持有 TdLib 客户端但未实现 ShutdownAsync

**证据**：`TDLSharpPlugin.cs:22-34` 注册 `TdlClientManager`（含 logger + 6 项设置），但全仓库 `override.*ShutdownAsync` 命中 0。AGENTS.md 明确要求"应用退出需优雅关闭……确保 TdLib 客户端正确释放"，实际未落实。

**建议**：`TdlClientManager` 实现 `IAsyncDisposable`；`TDLSharpPlugin` override `ShutdownAsync` 触发释放；宿主 `App.OnShutdownRequested` 确认调用链完整。

#### O-5. App.Initialize 三处 sync-over-async（UI 线程死锁风险）

**证据**：`App.axaml.cs:88, 91, 120` — `.GetAwaiter().GetResult()`。`PluginLoader` 内部曾修复同类问题（`PluginLoader.cs:92-95` 注释），但 App 启动路径仍是同步阻塞。

**建议**：启动序列整体改为 async（如 `InitializeAsync` + 闪屏期间 await），或至少收敛到单一同步点并加注释说明为何安全（Avalonia 启动期无嵌套泵时可接受，但应显式声明）。

### P1 — 结构与样板问题

#### O-6. 本地化注册 5 行样板 × 11 插件

**证据**：`ButtonsInputsPlugin.cs:22-26` 等 11 处完全相同模式（见 §1 共性表）。

**建议**：`MetadataGenerator` 按命名约定（`{PluginRoot}.Resources.Strings.ResourceManager` 存在则生成注册代码）自动生成；或宿主 `RegisterAllPluginsAsync` 后统一扫描插件程序集的 `ResourceManager` 并注册（批量注册同时解决 O-14 的缓存重建问题）。

#### O-7. 空 InitializeAsync override × 10（接口已有默认实现）

**证据**：`IPlugin.cs:16` 默认 `Task.CompletedTask`；10 个插件仍写空 override。

**建议**：直接删除。属于纯样板清理。

#### O-8. 元数据 csproj/代码双重声明（O-1 的结构根因）

**证据**：csproj `PluginName/Author/Description/Version/PluginId`（如 `Template.csproj:8-12`）与 `*Plugin.cs` 中 7 个 `IPluginMetadata` 属性并存，无任何同步机制。`GeneratePluginManifest` 目标读 csproj，`MetadataGenerator` 读代码——两套消费方各看一半。

**建议**：同 O-1，以 csproj 为单一事实来源，源生成器接管全部元数据属性。

#### O-9. MenuItemTreeBuilder.ParentIconMap 硬编码父菜单图标（破坏 OCP）

**证据**：`MenuItemTreeBuilder.cs:7-13` — 字典写死 `Leaf`、`NAV_NBData`、`NAV_ScottPlot`、`NAV_ProDataGrid` 四个 key。新插件自定义顶层父菜单图标必须修改 Shared 库。而 `[Menu]` 特性已有 `IconName` 命名属性（`MenuAttribute.cs:32`）却未被此路径消费。

**建议**：父菜单图标从子项 `[Menu(IconName=...)]` 聚合继承，删除硬编码字典。

#### O-10. ButtonsInputs 跨插件硬编码演示数据

**证据**：`AutoCompleteBoxDemoViewModel.cs:22-81` — `GetControlData()` 硬编码 60+ 项，涵盖 DateTime/DialogFeedbacks/LayoutDisplay/NavigationMenus 等**其他插件**的控件条目。任一插件增删演示项需手工同步此表。

**建议**：改为事件/聚合机制（各插件向共享 `ControlCatalogService` 注册自己的演示项），或至少把此表移出 ButtonsInputs 归宿主管理。

#### O-11. PluginEntry.Plugin 与 Metadata 双字段冗余

**证据**：`PluginLoader.cs:1324-1331` — 入口类同时实现 `IPlugin`（生成器补全）与 `IPluginMetadata`，反射查找后同一对象存两份引用。调用方混用两字段（如 `e.Plugin is IWebPlugin` vs `e.Metadata.Name`），语义割裂。

**建议**：`PluginEntry` 收敛为单引用（`IPlugin` 本身即可转型取元数据）；或生成器直接生成一个 `IPluginEntry` 聚合接口。

#### O-12. RegisterPluginNavigationAndMenus 异常处理时序晚

**证据**：`App.axaml.cs:191-227` — 导航/菜单注册在 ServiceProvider 构建与其他插件注册**之后**才执行；单插件抛异常时 `MarkPluginError`（line 224）已无法回收该插件此前注册的 DI 服务。

**建议**：导航/菜单定义获取（纯数据，无副作用）提前到 Discover/Initialize 阶段校验（生成器甚至可编译期生成静态清单），注册动作留在最后仅做写入。

#### O-13. LoadAllPluginManifests 状态重置一刀切

**证据**：`PluginLoader.cs:1101-1104` — `Loaded` 状态启动时强制重置为 `Installed`，随后全部重新加载。结合"无运行时增删"前提，`Disabled` 之外的所有插件（含 8 个纯演示插件）每次启动都完整走 ALC 创建 + 反射扫描，无按需/延迟加载路径。

**建议**（方向性，非立即实施）：演示类插件可标记 `LoadMode=Lazy`（首次导航命中才 Discover），清单阶段即可决策。依赖 BC-1 的 `kind` 字段扩展。

#### O-14. LocalizationService 逐插件重建缓存

**证据**：`LocalizationService.cs:102-103` — 每次 `RegisterResourceManager` 触发 `RebuildCacheAndSyncResources`；12 个插件顺序注册 = 12 次全量重建 + 12 次 `Application.Current.Resources` 同步。

**建议**：增加批量注册 API（`RegisterRange`）或延迟重建开关，宿主在 `RegisterAllPluginsAsync` 完成后统一重建一次。

### P2 — 死代码与过时约定

#### O-15. Dependencies 字段恒空且无消费方

**证据**：12/12 插件 `Dependencies => []`；清单构建期生成 `[]`（`LYBox.Plugin.Shared.targets:120`）；`DiscoverPluginAssemblyAsync`（`PluginLoader.cs:194-341`）从不读取。要么实现依赖校验（加载前检查依赖插件是否 Loaded），要么从契约中移除该字段。

#### O-16. GetIconResources 接口死代码

**证据**：`IPlugin.cs:31` 默认 `=> null`；12/12 插件未实现；宿主 `RegisterPluginNavigationAndMenus` 不调用。删除或落实消费方。

#### O-17. ViewLocator.InvalidateAllViewCache 无调用方

**证据**：`ViewLocator.cs:44-48`。结合"无热卸载"前提（AGENTS.md 明确运行时无需清理），连同 `MenuConfigurationService.RemoveMenuItem`（无插件调用）一并评估删除，避免误导后续开发者以为存在清理路径。

#### O-18. MetadataGenerator O(N²) 扫描

**证据**：`MetadataGenerator.cs:24-26` — 每个标注类命中时遍历整个 Compilation 全部 SyntaxTree；`GetFullTypeName`（line 162-172）仅按 `Identifier.Text` 匹配，跨命名空间同名类会误配。插件数量增长时编译性能退化。改用 `INamedTypeSymbol` 全限定名匹配 + 一次遍历建索引。

#### O-19. AGENTS.md / 文档与代码事实不符（汇总）

| 文档描述 | 代码事实 |
|----------|----------|
| AGENTS.md 插件列表含 `BlazorApp` | 源码已删（仅剩 bin/obj 残留） |
| AGENTS.md：PluginState 5 个状态 | 实际 7 个（多 `PendingUpgrade`、`Error`，`PluginState.cs:3-12`） |
| AGENTS.md：`@lybox/sdk` | 实际包名 `@lytree/sdk` |
| AGENTS.md：`LYBox.Plugin.Shared.Chart` / `.ProDataGrid` 子项目 | 不存在（`src/Plugin/` 下仅 Generators + Shared 两个项目） |
| `docs/WEB_PLUGIN_GUIDE.md` 等描述 `PluginWebAppService`/`SkipPluginWebBuild`/`dev-placeholder.html` 等 | 代码中均不存在（详见 `Plugin-SDK-Optimization-Analysis.md` §3.1） |

**建议**：AGENTS.md 事实修正应随下一次代码变更一并提交；过时 docs 移入 `docs/archive/` 或标注"历史设计"。

---

## 3. 优先级矩阵与建议实施顺序

| 优先级 | 编号 | 一句话 | 建议批次 |
|--------|------|--------|----------|
| P0 | O-1 + O-8 | PluginId/元数据单源化（源生成器接管） | 批次 1（配合 WebHost 方案 BC-7） |
| P0 | O-4 | TDLSharp ShutdownAsync + 释放链路 | 批次 1 |
| P0 | O-2 | BTSou 静态单例改 DI | 批次 1 |
| P0 | O-3 | NavigationService 缓存失效 Bug | 批次 1 |
| P0 | O-5 | App.Initialize sync-over-async | 批次 2 |
| P1 | O-6 | 本地化注册自动生成 | 批次 2（与 O-14 合并实现） |
| P1 | O-7 | 删空 override × 10 | 批次 2 |
| P1 | O-9 | 父菜单图标去硬编码 | 批次 2 |
| P1 | O-12 | 导航/菜单校验前置 | 批次 3 |
| P1 | O-14 | 本地化批量重建 | 批次 2 |
| P1 | O-11 | PluginEntry 收敛 | 批次 3 |
| P1 | O-10 | 演示目录去跨插件硬编码 | 批次 3 |
| P2 | O-15/O-16/O-17 | 死字段/死代码清理 | 批次 4 |
| P2 | O-18 | 生成器性能 | 批次 4 |
| P2 | O-19 | 文档事实修正 | 批次 4 |
| 方向 | O-13 | 懒加载插件 | 配合 WebHost 方案 S1 之后评估 |

> 每批次完成标准遵循仓库惯例：完整单元测试通过 + `.\build.ps1 --build=all` 全量构建 + 启动冒烟。

---

## 4. 测试策略建议

- **O-1**：单测断言"清单 PluginId == 插件实例 PluginId"（加载后全插件扫描），防回归；
- **O-3**：`OnPluginUnloaded` 触发后断言对应导航 key 缓存被清除；
- **O-4**：`ShutdownAllPluginsAsync` 后断言 TdLib 客户端销毁回调已执行（可用假实现）;
- **O-6/O-14**：批量注册后断言 `CultureChanged` / 缓存重建仅发生一次；
- **O-18**：生成器增量编译冒烟（两次连续 build，第二次应命中缓存）。
