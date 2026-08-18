# 开发说明
本文档面向项目开发和维护，说明构建、调试、CI、架构流程和插件开发约定。应用使用和插件安装说明请阅读 [使用说明](USAGE.md)；Web 插件完整操作流程见 [Web 插件使用手册](WEB_PLUGIN_GUIDE.md)，协议与宿主边界见 [Web 插件宿主与 IPC 约定](WEBVIEW_IPC.md)。

## 构建系统

项目使用 Cake Frosting 构建，入口为 `build/Program.cs`。推荐使用仓库根目录下的脚本：

```powershell
.\build.ps1 --build=all --configuration=Debug
```

常用构建目标：

```powershell
.\build.ps1 --build=bin
.\build.ps1 --build=nuget
.\build.ps1 --build=plugin
.\build.ps1 --configuration=Debug
.\build.ps1 --package-version=1.0.2
.\build.ps1 --platforms=windows,linux
```

构建产物：

| 目标 | 输出位置 |
| --- | --- |
| `bin` | `packages/bin` |
| `nuget` | `packages/nuget/Avalonia.Plugin.Generators.*.nupkg` 和 `packages/nuget/Avalonia.Plugin.Shared.*.nupkg` |
| `plugin` | `packages/plugins/<PluginName>/publish` 和 `packages/plugins/zip/*.zip` |

## Setting Up A New Cake.Sdk Project 构建

> 注：下述入口与上文"入口为 `build/Program.cs`（Cake Frosting）"的描述不同——当前仓库实际采用 **Cake.Sdk 文件化应用**，单一 C# 文件即整个构建项目。

Cake.Sdk 文件化应用是一种自引导的单文件构建：`build/build.cs` 通过 `#:sdk` 指令自动拉取运行器，无 cake.exe、无 `.cake` 脚本、无需 `.config/dotnet-tools.json` 工具清单。搭建一个新 Cake.Sdk 构建项目只需四步：

### 1. 前置条件
- .NET SDK（`global.json` 仅配置测试运行器，未锁定 SDK 版本；按需补 `<sdk><version>`）。
- 无需安装命令行工具；`dotnet-tools.json`（根目录，`"tools": {}`）可保持为空。

### 2. 新增构建脚本（最小骨架）
```csharp
#!/usr/bin/env dotnet
#:sdk Cake.Sdk@6.2.0       // 运行器版本
#:package Spectre.Console@0.57.2   // 可选附加包
#:property PublishAot=false

using Cake.Common;
using Cake.Common.Tools.DotNet.Build;

var target   = Argument("target", "Default");
var buildNumber = Argument("build-number", 0);

Task("Build")
    .Does(ctx => {
        ctx.DotNetBuild("./src/MyApp/MyApp.csproj",
            new DotNetBuildSettings { Configuration = "Release" });
    });

RunTarget(target);
```

约定：
- 用 `Argument("名称", 默认值)` 接收命令行（`--名称=值`）；复杂项目可抽一个 `BuildContext` 类缓存已解析设置，并用 `[Flags] enum` 表示多目标（如仓库内 `BuildTarget`）。
- 任务可组合：`Task("Default")` 用 `.IsDependentOn("Build")` 串联别名。
- 结尾必须 `RunTarget(target);` 并把 `target` 设为 `Default`。

### 3. 封装启动脚本
仓库根提供 `build.ps1`（Windows）与 `build.sh`（Linux/macOS），仅转调文件脚本并透传参数与退出码。

```powershell
# build.ps1
Push-Location $PSScriptRoot
try { dotnet build/build.cs -- $args; exit $LASTEXITCODE }
finally { Pop-Location }
```

```bash
# build.sh（Linux/macOS）
#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
exec dotnet build/build.cs -- "$@"
```

### 4. 运行
```bash
.\build.ps1 --build=all            # Windows
./build.sh --build=all             # Linux/macOS
dotnet build/build.cs -- --build=all   # 免脚本直接调用
```

关键点：`--` 之后的参数由 Cake.Sdk 原样解析为 `Argument`，与 `build.cs` 内定义的键一一对应。首次运行按 `#:sdk` 自动获取运行器（需联网还原），后续幂等。

## 本地运行和调试

直接运行启动器：

```powershell
dotnet run --project src/App/LYBox.Launcher.Desktop
```

启动器会扫描应用基础目录下的默认插件目录，也支持通过环境变量追加插件发现目录：

```powershell
$env:AVALONIA_EXTRA_PLUGINS_PATH="E:\path\to\plugin\publish"
dotnet run --project src/App/LYBox.Launcher.Desktop
```

`.vscode/launch.json` 中的 VS Code 调试配置会预构建指定插件，并自动设置 `AVALONIA_EXTRA_PLUGINS_PATH`。

## Web 前端工作区

仓库根 `package.json` 固定 pnpm `11.15.1`，Node 要求为 `>=22.18.0`。这个下限来自当前锁定依赖的引擎要求；`.node-version` 的内容为 `22`，支持它的版本管理器可以自动切换，但仓库脚本不会替换系统 Node。

插件项目还原使用 `plugins/nuget.config` 中的本地源。全新克隆后的首次准备必须先生成本地 NuGet 包，再安装前端依赖：

```powershell
.\build.ps1 --build=nuget --configuration=Debug
pnpm install
```

Linux/macOS：

```bash
./build.sh --build=nuget --configuration=Debug
pnpm install
```

工作区包括 `frontend/packages/*` 与 `plugins/*/Web`。Template 使用 Vue/Vite，生产路由基准路径由 `@avalonia-template/vite-plugin-avalonia` 统一设置为 `/plugins/{normalizedSegment}/`，路由使用 `createWebHistory(import.meta.env.BASE_URL)`。

### 三种运行模式

| 模式 | 资源 | IPC | 开发能力 |
| --- | --- | --- | --- |
| 生产模式 | Launcher 的本机回环静态资源宿主 | 真实 .NET IPC | 无 HMR，关闭控制台转发 |
| WebView 开发模式 | 本机回环 Vite | 真实 .NET IPC | WebView 工具栏、HMR、受限开发控制台转发 |
| 浏览器模拟模式 | 浏览器 Vite | 模拟 IPC | 浏览器开发者工具、HMR，无 .NET 桥接/端点 |

这里的 WebView“开发模式”是宿主依据有效发现文件选择的 `PluginWebResourceMode.WebViewDev`。Vite 的 `import.meta.env.DEV` 在 WebView 开发模式和浏览器模拟模式中都会成立，不代表页面一定具有真实桥接。

推荐先在浏览器模拟模式中调试前端，再运行真实联调：

```powershell
pnpm dev:template
```

该命令依次执行带 `SkipPluginWebBuild=true` 的插件构建、启动 Vite、等待与本次租约精确匹配的发现文件、启动实际 Launcher 并设置 `AVALONIA_EXTRA_PLUGINS_PATH`、等待 Launcher 就绪握手。`Ctrl+C` 会清理本次拥有的 Launcher/Vite 进程树和匹配的发现/就绪文件。

发现文件当前包含 `pluginId`、`origin`、`processId`、`startedAt`、`leaseId`。宿主忽略未知的 `leaseId` 字段，但会校验插件、本机回环 HTTP(S) 来源、PID、启动时间和健康状态；编排器使用 `leaseId` 保证所有权。

### VS Code 附加调试

选择 `Web Plugin: Template (orchestrated)`。后台 task 在收到实际 Launcher 的 `AVALONIA_WEB_DEV_READY <pid>` 前不会 ready；随后在 picker 中选择 PID 与该输出 `<pid>` 完全一致的 Avalonia Launcher 进程。

停止附加调试可能只断开调试器。调试完成后应停止 `dev-plugin-template-web` 后台任务，或在其终端中按 `Ctrl+C`，确保 Vite、Launcher 和发现文件完成清理。

## 解决方案结构

| 文件 | 内容 |
| --- | --- |
| `Core.slnx` | Generators、Shared、UI、Launcher.Desktop、Platforms.Abstractions |
| `Plugins.slnx` | Generators、Shared、部分插件项目 |

注意事项：

- `.slnx` 是 .NET 10 XML 解决方案文件，不是传统 `.sln` 文件。
- `Plugins.slnx` 不包含所有插件；构建脚本会动态发现 `plugins/*/*.csproj`。
- `src/Platforms` 下的项目引用仓库外部平台项目，默认不参与启动器和插件构建。

## 架构流程

应用启动采用分阶段插件生命周期：

1. 通过 `ServiceCollection.AddAvaloniaServices()` 注册核心服务。
2. 发现插件程序集并创建 `IPlugin` 实例。
3. 允许插件向共享 `IServiceCollection` 注册自己的服务。
4. 构建根 `IServiceProvider`。
5. 通过 EF Core migrations 初始化应用数据库。
6. 允许插件注册本地化等运行时资源。
7. 注册插件导航项和菜单。
8. 显示启动窗口，然后进入主窗口。

插件主生命周期：

```text
Installed -> Discovered -> Loaded -> Registered
```

其他状态包括 `Disabled`、`PendingUninstall`、`PendingUpgrade` 和 `Error`。

## 插件开发

建议以 `plugins/Avalonia.Plugin.Template` 作为新插件起点。一个插件通常需要：

- 目标框架设置为 `net10.0`。
- 以分析器方式引用 `Avalonia.Plugin.Generators`。
- 以 `PrivateAssets="all"` 引用 `Avalonia.Plugin.Shared`。
- 在 MSBuild 中定义插件元数据：`PluginId`、`PluginName`、`PluginAuthor`、`PluginDescription`。
- 实现标记了 `[GenerateMetadata]` 的 `IPluginMetadata` partial 类。
- 使用 `[NavigationItem]`、`[Menu]`、`[ViewMap]` 标记可导航 ViewModel。
- 在 `InitializeAsync(IServiceCollection services)` 中注册服务。
- 在 `RegisterAsync(IServiceProvider serviceProvider)` 中注册本地化等运行时资源。

最小包引用示例：

```xml
<PackageReference Include="Avalonia.Plugin.Generators" Version="1.0.5"
  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
<PackageReference Include="Avalonia.Plugin.Shared" Version="1.0.5-beta.10" PrivateAssets="all" />
```

这些版本应与插件项目和本地源一致；`packages/nuget/` 当前包含 `Avalonia.Plugin.Generators.1.0.5.nupkg` 与 `Avalonia.Plugin.Shared.1.0.5-beta.10.nupkg`。

构建或还原插件前，先生成本地插件依赖包：

```powershell
.\build.ps1 --build=nuget --configuration=Debug
```

如果插件需要加载 Vue/Vite 单页应用，并通过 WebView 调用 .NET 业务能力，请先阅读 [Web 插件使用手册](WEB_PLUGIN_GUIDE.md)，再参考 [Web 插件宿主与 IPC 约定](WEBVIEW_IPC.md)。当前模式由 `plugin.json.web { root, entry }` 声明资源，导航页面继承 `PluginWebRouteViewModel` 并映射到 `PluginWebViewPage`；业务调用使用真实 IPC，浏览器模拟仅用于浏览器前端调试。

## 插件打包约定

- 插件包由构建输出生成，格式为 `{Name}-{Version}.zip`。
- `plugin.json` 由共享 MSBuild targets 自动生成。
- 设置 `PluginWebRoot` 后会生成 `plugin.json.web`；发布目录应包含对应的 `web/index.html` 与 assets。
- 插件发布时，宿主已提供的共享框架程序集会由 `Avalonia.Plugin.Shared` 的构建目标从插件输出中剔除。
- 插件安装包应通过 `.\build.ps1 --build=plugin` 生成，不要手工压缩 `bin` 目录。

Template 的默认 `dotnet build` 会运行 `pnpm build` 并复制真实 `Web/dist`；`SkipPluginWebBuild=true` 的普通构建只生成开发占位页，供编排器加载插件 DLL。发布始终要求真实 `dist/index.html`，跳过 Web 构建且分发文件缺失时会报错。

## GitLab CI

仓库提供 `.gitlab-ci.yml`，流水线只由 tag 触发，并把主体和插件构建拆开：

```bash
git tag main-v1.0.2
git push origin main-v1.0.2

git tag plugin-leaf-v1.0.2
git push origin plugin-leaf-v1.0.2
```

主体流水线匹配 `main-v*` 或 `core-v*`，在一个主体 job 中同时打包 Windows 和 Linux：

- `build:main`：执行 `bin` 构建，产物为 `packages/bin/win-x64`、`packages/bin/win-arm64`、`packages/bin/linux-x64` 和 `packages/bin/linux-arm64`。

单插件流水线匹配 `plugin-<name>-v*`，只构建指定插件。插件构建使用构建脚本自动生成本地 `Avalonia.Plugin.Generators` 和 `Avalonia.Plugin.Shared` 依赖包，不发布也不读取 GitLab NuGet Package Registry。

`<name>` 不限制为当前已有插件；新增插件只要能被构建脚本通过项目名、短名、`PluginId` 或 `PluginName` 匹配，就可以使用同一套 tag 规则，例如 `plugin-leaf-v1.0.2`、`plugin-newplugin-v1.0.2`。

CI 使用带有 `image` tag 的 GitLab Runner，不声明容器镜像；构建时通过 `dotnet-install.sh` 安装 .NET 10 SDK。构建配置为 `Release`，`PACKAGE_VERSION=1.0.2`。CI 默认 `SELF_CONTAINED=true`，并串行执行 Windows/Linux 主体打包，降低 runner 内存峰值。CI 设置了 `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`，用于兼容未安装 ICU 的打包环境。

如果升级插件引用的 `Avalonia.Plugin.Shared` 或 `Avalonia.Plugin.Generators` 版本，需要同步调整 GitLab 变量 `PACKAGE_VERSION`。

## 维护说明

- Web 插件宿主测试位于 `tests/Avalonia.Plugin.Web.Tests`，前端进程树测试位于 `frontend/tools/windows-process-tree.test.mjs`；按对应验证流程执行，不要从文档修改本身推断测试已通过。
- 当前没有统一格式化或静态检查命令。
- 默认文化区域为 `zh-CN`。
- 修改插件共享契约后，需要同步提升内部包版本，并更新插件 `.csproj` 引用。
- 修改插件生命周期或安装状态时，需要同步更新使用文档和插件管理界面的状态展示。
- 禁用会清理 Web/IPC/运行时；启用当前只恢复为 `Installed`，需要重启 Launcher 才重新加载，不支持运行中热启服务或重建根依赖注入容器。
- 文档中的验证命令供贡献者按需运行；完整验证应覆盖相应 .NET、前端与运行时检查，不能只依据文档静态检查推断构建通过。
