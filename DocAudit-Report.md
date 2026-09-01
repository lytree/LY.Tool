# LYBox 使用文档一致性分析报告

> 分析对象：`README.md`、`AGENTS.md` 与 `docs/` 下的使用文档
> 对比基线：仓库实际代码、构建脚本（`build/build.cs`）、`.github/workflows/`、各 `csproj` 与 `git log`
> 结论：**文档明显滞后于代码**，停留在「WebView 前端 monorepo / 新增 WebView 插件 / 测试 / CI 引入」之前。README 与 AGENTS.md 均存在事实性错误与重大遗漏，二者之间也存在内部矛盾。

> **修订状态（2026-08-13）**：文档与构建布局已完成更新，废弃的独立窗口项目及其构建、CI、VS Code 配置均已删除。

***

## 一、事实性错误（与代码直接冲突，必须改）— P0

| # | 位置                                         | 文档说法                                                           | 实际情况                                 | 证据                                                                                                                                                                                                                                                                                                                                             |
| - | ------------------------------------------ | -------------------------------------------------------------- | ------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1 | `README.md` 第 38 行                         | 构建系统 "Cake 6.1.0"                                              | 实际 `Cake.Sdk 6.2.0`（`AGENTS.md` 正确）  | `build/build.cs:2` → `#:sdk Cake.Sdk@6.2.0`                                                                                                                                                                                                                                                                                                    |
| 2 | `README.md` 第 55 行                         | "无测试，无 CI 工作流"                                                 | 实际有测试 + 4 个 CI workflow              | `tests/LYBox.Tests/`（`TUnit 1.63.0`）；`.github/workflows/`：`ci.yml`、`release-host.yml`、`release-plugins.yml`、`release-frontend.yml`                                                                                                                                                                                                             |
| 3 | `README.md` 第 98 行 / `AGENTS.md` 第 50、77 行 | "10 个内置示例插件"                                                   | 实际 **12 个**插件                        | `plugins/` 下多出 `LYBox.Plugin.BlazorApp`、`LYBox.Plugin.WebTemplate`（均为 WebView 插件）                                                                                                                                                                                                                                                              |
| 4 | `README.md` 第 70–76 行 / `AGENTS.md` 架构表    | src/ 仅列 5 个项目                                                  | 实际 **9 个** src 项目                    | 缺失 `LYBox.Layout.Core`、`LYBox.Plugin.Shared.Chart`、`LYBox.Plugin.Shared.ProDataGrid`                                                                                                                                                                                                                                                           |
| 5 | `AGENTS.md` 应用启动流程                         | `LoadPluginsAsync()` 单阶段（且 `ServiceLocator.Initialize` 在插件加载前） | 实际为 3 阶段 + 导航/菜单注册（`README.md` 描述正确） | `src/launcher/LYBox.Launcher.Desktop/App.axaml.cs`：`DiscoverAllPluginAssembliesAsync` → `InitializeAllPluginsAsync(services)` → `BuildServiceProvider` → `ServiceLocator.Initialize` → `InitializeDatabase` → `InitializeLocalization` → `RegisterAllPluginsAsync` → `RegisterPluginNavigationAndMenus` → `OnFrameworkInitializationCompleted` |

***

## 二、文档重大遗漏（代码有，文档没提）— P1

1. **前端 monorepo（`frontend/`）完全未提及**

   - 实际存在：`frontend/`（pnpm workspace，根包 `lybox-frontend` v2.2.1-preview\.3），含 npm 包 `@lybox/sdk`、`create-lybox-react`、`create-lybox-vue3`，描述为「LYBox 前端 SDK 与脚手架模板 monorepo」。

   - 这是 WebView 插件体系的基础，但 `README.md` / `AGENTS.md` 的「架构概览」中无任何说明。

2. **WebView 相关文档成为「孤儿文档」**

   - `docs/WebView-IPC-Guide.md`（44KB）已在仓库中，但 **`README.md`** **的「详细文档」表与** **`AGENTS.md`** **均未引用它们**，新读者无法发现。

3. **`tools/LYBox.MockServer`（lybox-mock dotnet tool）已被移除**

   - 该 dotnet tool 及对应 `--build=tool` 构建目标、`docs/LYBox-MockServer-Guide.md` 已从仓库整体移除。

4. **构建目标清单不全**

   - 两文档均未记录以下实际支持的参数：`--host-version`（README 未列）、`--plugin-version`、`--plugin=<Name>`、`--nuget-source`。

   - `AGENTS.md` 记录了 `--host-version` / `--package-version`，但两文档都漏了 `plugin-version` / `plugin` / `nuget-source`。

5. **新增共享项目未说明**

   - `LYBox.Plugin.Shared.Chart`（供 ScottPlot 插件）、`LYBox.Plugin.Shared.ProDataGrid`（供 ProDataGrid 插件）的存在、作用与所属解决方案（应在 `Plugins.slnx`）均未写入文档。

***

## 三、文档内部矛盾（README 与 AGENTS 互相不一致）

| # | 矛盾点              | README                     | AGENTS                                                                                        | <br />                    | <br /> | <br />               |
| - | ---------------- | -------------------------- | --------------------------------------------------------------------------------------------- | :------------------------ | :----- | :------------------- |
| 1 | 构建系统命名           | "Cake Frosting"            | "Cake Frosting（…Cake.Sdk 6.2.0）"，而 `build.cs` 注释明确「符合官方 Cake.Sdk 项目设置模式，不再继承 FrostingContext」 | 建议统一为 **Cake.Sdk**        | <br /> | <br />               |
| 2 | `--build=all` 含义 | "bin（启动器 + NuGet）+ plugin" | 同上                                                                                            | 实际 \`All = Bin            | NuGet  | Plugin\`，README 文档准确 |
| 3 | 应用启动流程           | 3 阶段（准确）                   | `LoadPluginsAsync()` 单阶段（过时）                                                                  | 以 README / 代码为准，更新 AGENTS | <br /> | <br />               |
| 4 | Cake 版本          | 6.1.0（错）                   | 6.2.0（对）                                                                                      | 统一为 6.2.0                 | <br /> | <br />               |

***

## 四、次要 / 措辞问题

- `README.md` 第 45 行 `--package-version=1.2.3 # 设置版本（默认：1.0.0）`：实际 `build.cs` 默认值为空字符串（""），"默认 1.0.0" 来自插件 `PluginVersion` 的 fallback，建议措辞改为「覆盖所有层版本」。

- `README.md` 第 350 行称 `AVALONIA_EXTRA_PLUGINS_PATH` 用于「热加载调试」，但前文「插件系统前提约束」明确 **不支持热加载**。属开发期路径加载，建议改为「开发期临时加载」以免歧义。

***

## 五、建议修复优先级与动作清单

**P0（事实错误，必须立即改）**

- [ ] 修正 `README.md` Cake 版本 6.1.0 → 6.2.0（第 38 行）。

- [ ] 删除 `README.md` 第 55 行「无测试，无 CI 工作流」错误描述，改为指向 `tests/LYBox.Tests` 与 `.github/workflows`。

- [ ] 更新插件清单：10 → 12，补充 `BlazorApp`、`WebTemplate`（README 第 98 行、AGENTS 第 50/77 行）。

- [ ] 更新 `src/` 项目清单，补充 `LYBox.Layout.Core`、`LYBox.Plugin.Shared.Chart`、`LYBox.Plugin.Shared.ProDataGrid`（README 第 70–76 行、AGENTS 架构表）。

- [ ] 以代码为准校正 `AGENTS.md` 应用启动流程（3 阶段 + 导航/菜单注册）。

**P1（重大遗漏，建议补）**

- [ ] 在「架构概览」新增「前端 monorepo（frontend/）」与「WebView 插件体系」小节。

- [ ] 在 `README.md` 与 `AGENTS.md` 文档索引中补入 `docs/WebView-IPC-Guide.md`。

- [ ] 补全构建目标说明：`--host-version`、`--plugin-version`、`--plugin`、`--nuget-source`。

- [ ] 说明 `LYBox.Plugin.Shared.Chart` / `Shared.ProDataGrid` 与对应插件的关系。

**P2（一致性清理）**

- [ ] 统一构建系统命名为「Cake.Sdk」。

- [ ] 修正 `--build=all` 说明为 `Bin | NuGet | Plugin`。

***

## 六、总结

文档目前最大的问题是 **「前端 / WebView 化」重构未被文档吸收**：新增的 `frontend/` monorepo、两个 WebView 插件（`BlazorApp`/`WebTemplate`）、相关共享项目与 WebView 文档，在 `README.md` 与 `AGENTS.md` 中几乎完全缺席（`tools/LYBox.MockServer` 与 `docs/LYBox-MockServer-Guide.md` 现已整体移除）；同时插件数量（10→12）、src 项目（5→9）、测试/CI 的存在性、Cake 版本、启动流程等均有事实错误。建议按 P0→P1→P2 顺序修订，并将 WebView 文档纳入索引以消灭孤儿文档。
