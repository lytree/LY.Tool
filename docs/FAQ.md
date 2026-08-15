# 常见问题（FAQ）
本文档汇总插件开发与运行过程中常见问题及排查思路。

---

## 目录

- [Q1：插件加载失败，状态显示 `Error`？](#q1插件加载失败状态显示-error)
- [Q2：插件菜单/导航没显示？](#q2插件菜单导航没显示)
- [Q3：插件 Settings 页面看不到我注册的设置项？](#q3插件-settings-页面看不到我注册的设置项)
- [Q4：插件中如何使用第三方 NuGet 包？](#q4插件中如何使用第三方-nuget-包)
- [Q5：插件如何注册全局快捷键？](#q5插件如何注册全局快捷键)
- [Q6：插件之间如何通信？](#q6插件之间如何通信)
- [Q7：如何调试源生成器？](#q7如何调试源生成器)
- [Q8：为什么 `PluginLoadContext` 标记 `isCollectible=true` 但不调用 `Unload()`？](#q8为什么-pluginloadcontext-标记-iscollectibletrue-但不调用-unload)

---

## Q1：插件加载失败，状态显示 `Error`？

**A**：检查错误信息。最常见原因：

- `MinPluginSdkVersion` 不兼容 → 升级宿主或联系插件作者
- 缺少依赖（`FileNotFoundException`）→ 检查 `shared-assemblies.txt` 是否被正确生成
- 程序集签名/版本冲突（`FileLoadException`）→ 检查插件是否引用了与宿主不同版本的共享程序集

---

## Q2：插件菜单/导航没显示？

**A**：检查以下几点：

1. ViewModel 上是否同时标注了 `[NavigationItem]` 和（可选）`[Menu]`？两个 key 必须一致才能跳转。
2. `[ViewMap(typeof(TView))]` 是否标注？未标注则 `ViewLocator.Build` 返回错误占位符。
3. `plugin.json` 的 `state` 是否为 `Loaded`？其他状态不会注册到导航/菜单。
4. 查看 `App.axaml.cs` 中 `RegisterPluginNavigationAndMenus` 是否捕获了异常并标记 `PluginState.Error`。

---

## Q3：插件 Settings 页面看不到我注册的设置项？

**A**：

1. 确认在 `RegisterAsync` 中调用了 `ISettingsService.RegisterSettings`（不是 `InitializeAsync`，那时 `IServiceProvider` 还没构建）。
2. 确认 `pluginId` 参数与 csproj 中的 `<PluginId>` 一致。
3. 设置项的 `Group` 决定显示分组；空字符串组不会显示。

---

## Q4：插件中如何使用第三方 NuGet 包？

**A**：

1. 在插件 csproj 中 `<PackageReference Include="YourPackage" Version="x.y.z" />`。
2. 若该包的类型出现在插件公共 API（被 `LYBox.Plugin.Shared` 引用），需加入 `shared-assemblies.txt` —— 但通常不应这样做，应保持插件私有。
3. 默认情况下，第三方包作为插件私有依赖，由 `PluginLoadContext` 在插件 ALC 中加载，与宿主隔离。
4. 若包内含原生库（如 `libsqlite3.so`），需检查 `CleanPublishedPluginOutput` target 是否误删 —— 该 target 已排除已知共享原生库（`e_sqlite3`、`libSkiaSharp` 等）。

---

## Q5：插件如何注册全局快捷键？

**A**：当前 SDK 未提供全局快捷键 API。可通过 `KeyGestureInput` 控件让用户自定义快捷键，但实际绑定需通过 Avalonia `KeyBinding` 在宿主窗口层完成 —— 这需要扩展 `IPlugin` 接口或新增 `IHotkeyService`。

---

## Q6：插件之间如何通信？

**A**：当前 SDK 未提供插件间通信的官方机制。可选方案：

1. 通过 `IServiceCollection` 注册共享服务（在 `InitializeAsync` 中），其他插件通过 DI 获取。
2. 通过 `WeakReferenceMessenger.Default` 发送/接收消息（无强类型契约，慎用）。
3. 通过 `ISettingsService` 共享状态（适合配置项，不适合实时通信）。

推荐方案 1，但需注意插件加载顺序（当前按目录字母序），若插件 A 依赖插件 B 的服务，B 必须先加载。

---

## Q7：如何调试源生成器？

**A**：

1. 在插件项目设置 `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>`。
2. 构建后查看 `obj/Generated/` 目录下的 `.g.cs` 文件。
3. 源生成器项目本身可在 `src/Plugin/LYBox.Plugin.Generators/` 中调试，启动设置为 "Roslyn Component"。

---

## Q8：为什么 `PluginLoadContext` 标记 `isCollectible=true` 但不调用 `Unload()`？

**A**：这是有意为之的设计（参见 [插件系统前提约束](../AGENTS.md#插件系统前提约束强制)）：

- `isCollectible=true` 允许 ALC 卸载，但当前架构选择不在运行时卸载。
- 保留该标志是为了未来若启用热卸载，无需破坏性改动 ALC 构造逻辑。
- 应用退出时 `PluginLoader.Dispose` 会调用 `Unload()`，主要用于清理诊断信息，不影响功能。
