# Plugin SDK 依赖与实现简化分析

> 状态：分析完成（仅文档，未实施）
> 日期：2026-08-16
> 范围：C# SDK（`src/Plugin/LYBox.Plugin.Shared` + `LYBox.Plugin.Generators`）与前端 SDK（`frontend/packages/`）
> 关联：`docs/WebHost-Optimization-Design.md`（BC-4 拆分方案的依据）、`docs/Plugin-Implementation-Analysis.md`

---

## 1. C# SDK（LYBox.Plugin.Shared）现状

### 1.1 依赖清单（`LYBox.Plugin.Shared.csproj`）

| 包 | 版本 | 用途（csproj 注释） | 评估 |
|----|------|--------------------|------|
| Avalonia | 12.1.1 | 公共 API 暴露 Control 类型 | 必要 |
| Avalonia.Skia | 12.1.1 | 公共 API 暴露 Skia 类型 | 必要 |
| Microsoft.Extensions.DI | 10.0.10 | `IPlugin.InitializeAsync` 参数 | 必要 |
| CommunityToolkit.Mvvm | 8.4.2 | ViewModelBase | 必要 |
| Irihi.Ursa | 2.2.0 | SystemCommands 用 OverlayMessageBox | 必要 |
| Microsoft.EntityFrameworkCore | 10.0.10 | DbContext/DbSet 抽象 | 必要（插件设置/持久化） |
| **System.Reactive** | 7.0.0 | **未说明（见 S-1）** | 存疑 |
| ProDataGrid | 12.0.4 | 公共表格组件（共享清单） | 必要 |
| **Avalonia.Controls.WebView** | 12.0.1 | WebView 承载 | **仅 Web 插件需要（S-2）** |
| **FrameworkReference: Microsoft.AspNetCore.App** | — | Kestrel/SSE | **仅 Web 插件需要（S-2）** |

显式锁定的递归依赖（6 项，运行时由宿主提供）：`DI.Abstractions`、`Options`、`Primitives`、`Logging.Abstractions`、`Microsoft.Bcl.AsyncInterfaces`、`SkiaSharp`、`HarfBuzzSharp`、`Irihi.Avalonia.Shared`。

### 1.2 API 表面（按目录）

| 目录 | 内容 | 问题信号 |
|------|------|----------|
| 根 | IPlugin、IPluginMetadata、ViewModelBase、ViewLocator、ServiceLocator、MenuItemTreeBuilder、TaskScope、ToolBarItemViewModel | IPlugin.cs 耦合 ToolBarItemViewModel 系列（与契约无关） |
| Attributes/ | 5 个生成器特性 | 正常 |
| Rpc/ + Web/ | IPC 运行时 + Web 承载 + HTTP 服务 | 仅 Web 插件消费（S-2 拆分对象） |
| Services/ | 7 个服务接口 | 正常 |
| Models/ | PluginInfo/Manifest/State 等 | 正常 |
| Converters/ + DataTemplates/ | 10+ 转换器、2 选择器 | 部分仅宿主使用？待消费方审计 |
| Dialogs/ | CustomDemoDialog、DefaultDemoDialog（含 VM） | **演示对话框混入 SDK（S-4）** |
| ViewModels/ | MenuItemViewModel、MenuViewModel | 正常 |

---

## 2. C# SDK 可简化/优化点

### S-1. System.Reactive 依赖用途不明【中，立即核查】

**证据**：`LYBox.Plugin.Shared.csproj:51` 引用且列入共享程序集清单（`LYBox.Plugin.Shared.props:30` 附近），但 csproj 的依赖注释块（23-42 行）唯独未说明它。需核查公共 API 是否真的暴露 `IObservable` 相关类型（`Channel<T>` 若仅用自定义实现则可移除）。

**建议**：无公共 API 暴露 → 移除；有 → 评估替换为 `System.Threading.Channels`（BCL 内置）。

### S-2. SDK 单体拆分【高，已在 WebHost 方案 BC-4 立项】

11 个非 Web 插件被迫间接引用 `Avalonia.Controls.WebView` + `Microsoft.AspNetCore.App`。拆分方案见 `docs/WebHost-Optimization-Design.md` §3.2（核心包 + `Shared.Web` 包），此处不重复。**本分析补充的拆分收益数据**：

- 非 Web 插件发布目录可减少 ASP.NET Core 框架引用的共享清单同步负担；
- `CleanPublishedPluginOutput`（targets:43-88）的排除规则可按包裁剪，减少"插件 zip 里意外打进 AspNetCore dll"的审计面。

### S-3. IPlugin.cs 契约文件耦合无关类型【低】

**证据**：`IPlugin.cs:50-86` 定义 `ToolBarItemViewModel` 及其派生（与插件契约无关）。契约文件应只含契约，工具栏 VM 移至 ViewModels/。

### S-4. Dialogs/ 演示对话框不应在 SDK【低】

**证据**：`Dialogs/CustomDemoDialog.*`、`DefaultDemoDialog.*` 从命名即为演示用途。SDK 公共 API 表面被演示代码污染。

**建议**：移至 `LYBox.Plugin.DialogFeedbacks`（演示插件）或宿主 Layout；SDK 移除后做二进制兼容检查（仅宿主与 DialogFeedbacks 引用则无影响）。

### S-5. IRpcBindingSource.TsDeclarations / JsGlue 死属性【中】

**证据**：`IRpcHost.cs:33-36` 声明两个 static abstract 属性；`RpcCommandGenerator.cs:88-110` 生成完整内容（TS 声明逐字串 + 命令清单 JSON）；但全仓库无任何运行时消费方——`WebPluginBindings.Register`（`WebPluginBindings.cs:26-43`）只调 `RegisterBindings`，`InjectBindingsAsync`（`WebViewIpcHost.cs:74-81`）自行用 `_commands.Keys` 序列化，且 `ipc.js` 的 `setBindings` 已是 noop（`ipc.js:131-133`）。

**建议**（二选一）：
- 删除两个属性 + 生成器对应产出（最小改动）；
- 或落实其设计意图：构建期把 `TsDeclarations` 生成 `.d.ts` 随 SDK 分发（`create-lybox-*` 模板引用），打通"后端命令 → 前端类型"链路。推荐后者——这是当前前端只能 `rpc<any>(name)` 的根因。

### S-6. 共享程序集清单两处手工同步【中】

**证据**：`LYBox.Plugin.Shared.props:28-59`（`_SharedAssembliesPatterns` 字符串清单）与 `LYBox.Plugin.Shared.targets:9-37`（`StartsWith`/`==` 硬编码条件）维护同一份清单的两种表达，已发现漂移（targets 精确匹配列表缺 `System.Reactive`）。

**建议**：单一源（一个 .props item group，如 `<SharedAssemblyPattern Include="System.Reactive*" />`）→ props 与 targets 均消费 items；构建目标从 items 生成 `shared-assemblies.txt` 与排除条件。⚠️ 脚本实现语言遵循仓库规则：F#（`.fsx`，`dotnet fsi`），禁止 Python。

### S-7. RpcCommandAttribute 注释与实现矛盾【低】

**证据**：`RpcCommandAttribute.cs:17` 注释称前端经 `window.go.<Namespace>.<Class>.<Name>` 调用；生成器（`RpcCommandGenerator.cs:118-119`）与实际运行时（`WebTemplatePlugin` 前端 `window.__lybox.rpc('GreetAsync', ...)`）均为短名。修正注释，或随 S-5 方案 B 引入命名空间化命令名（破坏性，需同步 ipc.js 与 SDK）。

### S-8. PluginSdkContract 版本注入链路正确但脆弱【信息】

`GeneratePluginSdkContract`（csproj:116-133）把 `$(HostVersion)` 编译进 `PluginSdkContract.g.cs`；三处 SDK 兼容校验（加载/升级/安装）依赖主版本号相等。链路正确；注意 `Directory.Build.props` 的 `LyboxLastReleasedVersion` fallback 会让 IDE 直接构建产出与 GitVersion 不同的版本——文档已记载，保持现状即可。

---

## 3. 前端 SDK（frontend/packages/）现状与问题

### 3.1 事实基线（多处与文档不符）

| 项 | 文档（AGENTS.md 等） | 实际 |
|----|---------------------|------|
| SDK 包名 | `@lybox/sdk` | **`@lytree/sdk`**（`frontend/packages/sdk/package.json:2`） |
| 脚手架包名/bin | `create-lybox-react` / `create-lybox-vue3` | **`create-lybox-react-template` / `create-lybox-vue3-template`** |
| `LYBox.Plugin.Shared.Chart` / `.ProDataGrid` 子项目 | 存在 | **不存在**（仅 Generators + Shared） |
| `docs/WEB_PLUGIN_GUIDE.md`、`WEBVIEW_IPC.md`、`DEVELOPMENT.md` 描述的 `PluginWebAppService`、`SkipPluginWebBuild`、`dev-placeholder.html`、`Web/dist → web/**` 自动化 | 描述详尽 | **代码中均不存在**（历史/未来设计混入现行文档） |

**建议**：包名二选一定案（`@lybox/*` 与产品一致更合理，属破坏性重命名，需与 npm 发布策略一起决策）；过时 docs 移入 archive。**注意**：若采纳重命名，`@lytree/sdk` 未发布过公开版本则零迁移成本——当前未发布，窗口期正合适。

### 3.2 依赖结构（本身已极简）

- `@lytree/sdk`：**零运行时依赖、零 peer 依赖**，devDeps 仅 tsup + typescript —— 依赖设计无问题；
- 两个脚手架：零依赖纯 Node ESM —— 无问题；
- 模板：react/vue3 + vite + SDK `^2.2.0`（SDK 实际 `2.2.1-preview.3`，semver 可匹配但建议显式对齐）。

### 3.3 前端可简化/优化点

#### F-1. SDK 公共 API 70%+ 无消费者【中，收缩或激活】

对比 `index.ts` 导出（30+ 符号）与三处消费方（WebTemplate 原生 `window.__lybox`、React/Vue3 模板）：

- **在用**：`rpc`、`on`、`isWebView`、`mountDebugPanel`、`setTheme`、`getTheme`、`restoreTheme`（7 个）；
- **未用**：`rpcChannel`、`createRpcClient`、`off`、`emit`、`whenReady`、`waitForLybox`、`getEnvironment`、`isBrowser`、`getBindings`、`getDebugInfo`、`toggleTheme`、`tokens`、`RpcError`、`createChannel`、`system.ts` 全部 5 个命令封装。

**建议**：不是简单删除——分两类处理：
1. **合理保留**（API 完整性需要）：`off/emit/whenReady/RpcError/createChannel` 属于 IPC 四件套对称 API；
2. **应激活**（当前是价值未兑现）：
   - `system.ts` 的 5 个类型化命令（`openFilePicker` 等）——WebTemplate 用裸 `rpc('OpenFilePicker',...)` 绕过了它们，应让 WebTemplate 示范使用 SDK 封装；
   - `createRpcClient`——配合 S-5 方案 B（`.d.ts` 生成）可升级为类型化客户端，这是 SDK 的核心卖点；
   - `emit`——宿主侧 `SystemCommands` 等能力的事件回推需要它做示范。

#### F-2. Design Token 三重硬编码，无单一数据源【中】

同一颜色值（如 `#0078D4`）在 4+ 处硬编码：`tokens.json:7`、`lybox-theme.css:23`（:root）、`:169`（dark）、`:281`（@media）。改一个色值需同步 4 处。

**建议**：以 `tokens.json` 为单一源，构建期生成 `lybox-theme.css` 的三个块（F# `.fsx` 脚本，`dotnet fsi build-tokens.fsx`，挂入 `pnpm build` prestep）。`lybox-components.css` 因只消费 CSS 变量不需生成。

#### F-3. lybox-components.css（337 行）零消费【中】

三个消费方全部自写样式且类名不一：WebTemplate 自定义 `.card/.badge`、React 模板 `.btn/.card`、Vue3 模板 `.lybox-btn/.lybox-card`。SDK 组件样式层是"死资产"。

**建议**（二选一）：
- 模板全面改用 `.ly-*` 类（含删除 Vue3 模板的 `.lybox-*` 重复定义），让组件层成为事实标准——推荐，同时解决三模板样式各异的碎片化；
- 或从 SDK 删除 components.css，明确定位"SDK 只提供 token，不做组件样式"。

#### F-4. 两脚手架 index.js 逐行重复【低】

`create-lybox-react/index.js`（48 行）与 `create-lybox-vue3/index.js`（51 行）仅默认项目名/端口提示不同。另有未使用导入（`copyFile`、`stat`）。

**建议**：抽取 `@lytree/create-utils`（或合并为单一脚手架 + `--template react|vue3` 参数）。脚手架语义上更贴近 `npm create` 生态，参数化单包是主流做法（参照 create-vite）。

#### F-5. SDK 源码注释引用不存在包名/子路径【低】

`lybox-theme.css:8`（`@lybox/theme/css`）、`lybox-components.css:9-10`（`@lybox/sdk/*`）、`debug.ts:43`（`@lytree/sdk/debug` 子路径不存在，exports 仅 `./theme`/`./css`/`./components`/`./tokens`）。随 F-1 定名一并修正；若要支持 `./debug` 子路径则补 exports。

#### F-6. 前端构建产物进插件链路断裂【高（体验），设计缺口】

现状：脚手架产物在 `dist/`，需手动拷入插件 `wwwroot/`（模板 README 明示手工步骤）；`build.cs` 只拷 `plugins/*/wwwroot/`，不执行 `pnpm build`、不识别 `dist/`。

**建议**：`WebHost-Optimization-Design.md` BC 引入 `PluginKind=Web` 后，可配套约定 `PluginWwwrootSource` 属性：值为 `wwwroot`（静态）或 `frontend-dist`（构建期执行 `pnpm --filter {pkg} build` 并拷贝 dist）。属该方案的后续扩展项，此处立项不展开。

---

## 4. 优先级矩阵

| 优先级 | 编号 | 项 | 建议批次 |
|--------|------|-----|----------|
| 高 | S-2 | SDK 拆分 Core/Web | 批次 1（= WebHost 方案 S3） |
| 高 | F-1(激活部分) + S-5(方案 B) | 命令类型化链路（TsDeclarations → .d.ts → createRpcClient） | 批次 2（前端 SDK 下一版本的核心价值点） |
| 中 | S-6 | 共享清单单源生成 | 批次 1（随拆分一并做） |
| 中 | S-1 | System.Reactive 核查 | 批次 1（一次 grep 即可定案） |
| 中 | F-2 | Token 单源生成 | 批次 2 |
| 中 | F-3 | 组件样式统一或删除 | 批次 2（与 F-2 同一 PR） |
| 中 | 3.1 | 包名定案 `@lybox/*` | 批次 1（未发布窗口期，越早越便宜） |
| 低 | F-4 | 脚手架合并 | 批次 3 |
| 低 | S-3 / S-4 / S-7 / F-5 | 契约文件整理、Demo 对话框迁出、注释修正 | 批次 3 |
| 设计缺口 | F-6 | 前端 dist → wwwroot 自动化 | 挂接 WebHost 方案后续扩展 |

---

## 5. 汇总：SDK "瘦身—增值"双轨结论

1. **瘦身**（减法）：拆出 Web 包（S-2）、清算 System.Reactive（S-1）、死属性二选一处置（S-5）、Demo 对话框迁出（S-4）、组件样式定去向（F-3）——目标是**非 Web 插件依赖图最小化**与**API 表面诚实**。
2. **增值**（加法）：打通 `RpcCommand → .d.ts → createRpcClient` 类型化 RPC 链路（S-5B + F-1）、Token 单源生成（F-2）、前端构建集成（F-6）——目标是让"用 SDK"比"裸调 window.__lybox"有**不可替代的收益**（类型安全 + 主题一致 + 零配置构建）。

当前最大的结构性矛盾：**WebTemplate（唯一 Web 插件示范）自己绕过了 SDK**（F-1 证据），导致 SDK 的类型化封装无从示范。任何 SDK 推广应从改造 WebTemplate 开始。
