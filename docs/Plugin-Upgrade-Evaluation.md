# 插件覆盖安装与升级方案评估

本文档评估在不支持热卸载的前提约束下，通过"临时目录 + 重启迁移"实现插件覆盖安装与版本升级方案的可行性、风险与实施建议。

---

## 目录

- [背景](#背景)
- [方案设计](#方案设计)
- [可行性评估](#可行性评估)
- [潜在问题与对策](#潜在问题与对策)
- [实现工作量评估](#实现工作量评估)
- [总结](#总结)

---

## 背景

由于 [插件系统前提约束](../AGENTS.md#插件系统前提约束强制) 不支持热卸载，已加载插件的 DLL 被进程锁定，直接覆盖安装会失败。当前实现（[`PluginInstallationManager.cs`](../src/LYBox.UrsaWindow/Services/PluginInstallationManager.cs)）的策略是：**检测到目标插件处于 `Loaded`/`Error` 状态时直接拒绝安装**，提示用户关闭应用后重试。

本节评估一个更友好的替代方案：**安装时先把新版本放到临时目录（或 pending 目录），应用重启时由宿主启动流程把新版本迁移到 `plugins/` 替换旧版本**。

---

## 方案设计

```
用户点击"升级"
    ↓
PluginInstallationManager 检测到目标插件已加载
    ↓
把新版本解压到 plugins/.pending/{PluginId}.new/
    ↓
写一个迁移指令文件 plugins/.pending/{PluginId}.upgrade.json
    ↓
提示用户"升级已就绪，重启应用以完成"
    ↓
==================== 应用重启 ====================
    ↓
PluginLoader 构造函数 → ProcessPendingUpgrades()
    ↓
删除 plugins/{PluginId}/  (此时 DLL 已释放)
    ↓
把 plugins/.pending/{PluginId}.new/ 移动到 plugins/{PluginId}/
    ↓
删除 plugins/.pending/{PluginId}.upgrade.json
    ↓
正常进入插件发现流程（加载新版本）
```

---

## 可行性评估

| 维度 | 评估 | 结论 |
|------|------|------|
| **DLL 锁释放** | 应用关闭后 `PluginLoadContext` 自然释放，DLL 不再被进程锁定。重启早期（`PluginLoader` 构造函数）即可安全删除/移动文件。 | ✅ 可行 |
| **迁移时机** | `PluginLoader` 构造函数已有 `ProcessPendingUninstalls()`，在同一位置加 `ProcessPendingUpgrades()` 顺理成章，且在任何插件加载之前执行。 | ✅ 可行 |
| **状态一致性** | 旧 manifest 的状态（如 `Disabled`、`InstallTime`）需迁移到新 manifest。可在 `.upgrade.json` 中记录"保留状态"指令，迁移时读取旧 manifest 合并到新 manifest。 | ✅ 可行（需设计状态合并策略） |
| **多版本并存** | 若用户连续点击两次升级（不同版本），`.pending/{PluginId}.new/` 会被覆盖，最后一个版本生效。可加版本号比较，仅保留最新。 | ✅ 可行 |
| **回滚** | 若新版本启动失败（如 `PluginState.Error`），可保留旧版本到 `.pending/{PluginId}.old/`，提供"回滚到上一版本"功能。 | ✅ 可行（需额外设计） |
| **跨平台路径** | Windows/Linux/macOS 的目录移动语义不同（Windows 同卷移动是 atomic rename，跨卷需复制）。建议统一用 `Directory.Move` + 失败时回退到复制+删除。 | ⚠️ 需处理跨平台 |
| **磁盘空间** | `.pending` 目录会临时占用额外空间（约等于插件大小 × 2）。大插件需提示用户。 | ⚠️ 小问题 |
| **权限** | 若 `plugins/` 在 `Program Files` 下，普通用户无写权限，需 UAC 提权。本模板默认部署在用户目录，无此问题。 | ✅ 默认场景可行 |

---

## 潜在问题与对策

1. **重启前用户取消升级**
   - **问题**：用户点击升级后改变主意，不希望重启后生效。
   - **对策**：提供"取消待升级"API，删除 `.pending/{PluginId}.upgrade.json` 与 `.pending/{PluginId}.new/`。UI 显示"待升级"状态时附带取消按钮。

2. **应用启动时迁移失败**
   - **问题**：迁移过程中磁盘满/权限错误，导致旧版本已删除但新版本未就位。
   - **对策**：采用 **先复制后删除** 顺序：
     ```
     1. 复制 .pending/{PluginId}.new/ → plugins/{PluginId}.new/
     2. 重命名 plugins/{PluginId}/ → plugins/{PluginId}.old/
     3. 重命名 plugins/{PluginId}.new/ → plugins/{PluginId}/
     4. 删除 plugins/{PluginId}.old/
     ```
     每一步失败都可回滚到上一步状态。

3. **多个插件同时待升级**
   - **问题**：用户连续升级多个插件，重启时需依次处理。
   - **对策**：`ProcessPendingUpgrades()` 遍历 `.pending/*.upgrade.json` 逐个处理。单个失败不影响其他插件，记录错误日志后继续。

4. **升级期间插件状态丢失**
   - **问题**：旧插件被标记为 `Disabled`，升级后新插件默认为 `Installed`，用户需重新禁用。
   - **对策**：`.upgrade.json` 中记录 `PreserveState: true`，迁移时读取旧 manifest 的 `State` 字段（仅 `Disabled`/`Installed` 合法，`Error`/`PendingUninstall` 重置为 `Installed`），写入新 manifest。

5. **MinPluginSdkVersion 校验时机**
   - **问题**：升级时已通过 [`PluginInstallationManager`](../src/LYBox.UrsaWindow/Services/PluginInstallationManager.cs) 中的 `IsPluginSdkCompatible` 校验，但用户可能在重启前升级了宿主到不兼容版本。
   - **对策**：迁移时再次调用 `IsPluginSdkCompatible`，若失败则不删除旧版本，仅删除 `.pending/` 中的新版本，并提示用户"新版本与当前宿主不兼容，已自动保留旧版本"。

6. **`.pending` 目录被外部删除**
   - **问题**：用户手动清理临时文件时误删 `.pending/`，导致重启后迁移失败但无提示。
   - **对策**：`ProcessPendingUpgrades()` 容错处理：若 `.upgrade.json` 存在但 `.new/` 目录缺失，记录告警日志并删除 `.upgrade.json`，不影响旧版本正常加载。

7. **并发安装与升级**
   - **问题**：用户在"待升级"状态下又点击"安装"（同插件不同版本），可能产生竞态。
   - **对策**：`PluginInstallationManager.InstallFromStreamAsync` 在检测到 `.pending/{PluginId}.upgrade.json` 已存在时，直接覆盖 `.pending/{PluginId}.new/` 并更新 `.upgrade.json` 中的版本号，保证原子性。

---

## 实现工作量评估

| 改动点 | 涉及文件 | 复杂度 |
|--------|---------|--------|
| 新增 `PendingUpgradeInfo` 模型与 `.upgrade.json` 序列化 | `LYBox.Plugin.Shared/Models/`（新增） | 低 |
| `PluginInstallationManager` 增加 `ScheduleUpgradeAsync` 路径 | [`PluginInstallationManager.cs`](../src/LYBox.UrsaWindow/Services/PluginInstallationManager.cs) | 中 |
| `PluginLoader` 构造函数增加 `ProcessPendingUpgrades()` | [`PluginLoader.cs`](../src/LYBox.UrsaWindow/Services/PluginLoader.cs) | 中 |
| `PluginInfo` 增加 `PendingUpgrade` 状态字段 | `LYBox.Plugin.Shared/Models/PluginInfo.cs` | 低 |
| UI 显示"待升级"状态及取消按钮 | `PluginManagementViewModel` / 对应 View | 中 |
| 跨平台目录移动工具方法 | `LYBox.UrsaWindow/Services/`（新增 `PluginUpgradeMigrator.cs`） | 低 |

---

## 总结

**结论：方案技术上完全可行**，且与现有"无热卸载"前提约束高度契合 —— 所有破坏性操作都在应用未启动时完成，运行时无需任何 ALC 卸载/重载逻辑。

**优点**：

- 用户体验显著提升：无需手动关闭应用即可触发升级。
- 实现成本低：复用现有 `ProcessPendingUninstalls` 模式。
- 失败可回滚：通过"先复制后删除"保证数据安全。

**缺点 / 风险**：

- 需新增"待升级"中间状态及对应 UI。
- 跨平台目录移动需小心处理（特别是 Windows 跨卷）。
- 用户可能长时间不重启，导致 `.pending/` 占用磁盘空间。

**建议实施顺序**：

1. 先实现核心迁移逻辑（`ProcessPendingUpgrades` + `ScheduleUpgradeAsync`），不带回滚。
2. 加上"先复制后删除"的安全迁移顺序。
3. 最后补充"取消待升级"、状态合并、UI 提示等增强功能。
