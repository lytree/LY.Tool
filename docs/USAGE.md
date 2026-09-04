# 使用说明
本文档面向应用使用和现场部署，说明启动程序、安装插件、覆盖升级插件和运行限制。开发、构建和插件编码说明请阅读 [开发说明](DEVELOPMENT.md)。

## 启动程序

使用发布后的桌面程序时，找到 `Avalonia.Launcher.Desktop.exe`，双击启动。

开发调试时可使用：

```powershell
dotnet run --project src/App/LYBox.Launcher.Desktop
```

## 插件安装

插件安装面向已经打包好的插件 zip 包，推荐使用构建脚本生成安装包：

```powershell
.\build.ps1 --build=plugin
```

安装步骤：

1. 启动 `Avalonia.Launcher.Desktop.exe`。
2. 进入主界面的插件管理入口，点击“插件”。
3. 点击“安装插件”。
4. 在文件选择窗口中找到插件对应的 `.zip` 包并双击选择。
5. 等待插件安装完成。
6. 重启主程序，使插件安装生效。
7. 回到插件管理界面，确认插件状态为已安装、已加载或已注册。

安装注意事项：

- 不要直接选择插件目录或 DLL 文件，应选择插件 zip 包。
- 安装后未重启时，插件可能已经进入待生效状态，但菜单和页面不一定立即可用。
- 如果插件状态显示异常，优先检查 zip 包是否来自当前版本构建输出，以及插件依赖是否已经随主程序一起发布。

### 使用命令行管理插件

控制台启动器与 GUI 共用同一套插件管理服务。可在自动化部署或无图形界面的环境中执行：

```powershell
dotnet run --project src/App/LYBox.Launcher.Console -- plugins list
dotnet run --project src/App/LYBox.Launcher.Console -- plugins info <plugin-id>
dotnet run --project src/App/LYBox.Launcher.Console -- plugins install .\LYBox.Plugin.Example-1.0.0.zip
dotnet run --project src/App/LYBox.Launcher.Console -- plugins uninstall <plugin-id>
```

`install` 的别名为 `add`，`uninstall` 的别名为 `remove`。所有插件管理命令支持 `--output=json`，详细参数可通过 `--help` 查看。外部开发目录中的插件为只读状态，不能通过 GUI 或 CLI 覆盖、卸载、启用或禁用。

## 插件覆盖安装与升级

插件系统不支持热卸载。已加载插件的 DLL 会被当前进程锁定，因此运行中不能直接覆盖正式插件目录。


用户需要注意：

- 覆盖安装或升级后必须重启主程序。
- 如果连续选择同一个插件的多个新版本，最后一次选择的版本会作为待升级版本。
- 如果迁移失败，程序会尽量保留或恢复旧版本目录。
- 原插件处于禁用状态时，升级后仍会保持禁用状态。

## 插件状态说明

| 状态 | 说明 |
| --- | --- |
| `Installed` | 已安装，等待发现或重启后加载 |
| `Discovered` | 已发现程序集并创建插件实例 |
| `Loaded` | 插件已向服务容器注册服务 |
| `Registered` | 插件已完成运行时资源注册 |
| `Disabled` | 插件已禁用，不会加载 |
| `PendingUninstall` | 已标记卸载，重启后删除 |
| `PendingUpgrade` | 已标记升级，重启后替换 |
| `Error` | 插件加载、初始化或注册失败 |

## 运行环境限制

- 当前交付包面向 Windows 桌面运行环境。
- 支持 Windows Server 2016 以及更新版本。
- 支持 Windows 10 以及更新版本。
- 插件安装包必须来自当前项目构建产出的 `.zip` 文件。
- 插件变更通常需要重启主程序后才会完全生效。
- 涉及数据库或现场数据导入的插件，需要先确认现场环境、CMC 配置、数据库地址、账号和密码与实际部署一致。

## 插件文档

每个插件维护自己的使用文档：

| 插件 | README | 使用说明 | 开发说明 |
| --- | --- | --- | --- |
| Template | [README](../plugins/Avalonia.Plugin.Template/README.md) | [使用](../plugins/Avalonia.Plugin.Template/docs/USAGE.md) | [开发](../plugins/Avalonia.Plugin.Template/docs/DEVELOPMENT.md) |
| Leaf | [README](../plugins/Avalonia.Plugin.Leaf/README.md) | [使用](../plugins/Avalonia.Plugin.Leaf/docs/USAGE.md) | [开发](../plugins/Avalonia.Plugin.Leaf/docs/DEVELOPMENT.md) |
| NBData | [README](../plugins/Avalonia.Plugin.NBData/README.md) | [使用](../plugins/Avalonia.Plugin.NBData/docs/USAGE.md) | [开发](../plugins/Avalonia.Plugin.NBData/docs/DEVELOPMENT.md) |
| ProDataGrid | [README](../plugins/Avalonia.Plugin.ProDataGrid/README.md) | [使用](../plugins/Avalonia.Plugin.ProDataGrid/docs/USAGE.md) | [开发](../plugins/Avalonia.Plugin.ProDataGrid/docs/DEVELOPMENT.md) |
| ScottPlot | [README](../plugins/Avalonia.Plugin.ScottPlot/README.md) | [使用](../plugins/Avalonia.Plugin.ScottPlot/docs/USAGE.md) | [开发](../plugins/Avalonia.Plugin.ScottPlot/docs/DEVELOPMENT.md) |
