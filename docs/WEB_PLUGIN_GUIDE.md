# Web 插件使用手册

本文档是仓库中面向插件开发者的 Web 插件统一操作入口，按环境准备、快速体验、接入、调试、构建和发布的顺序说明当前支持的工作流。IPC 报文字段、错误码、安全状态机和生命周期细节以 [Web 插件宿主与 IPC 约定](WEBVIEW_IPC.md) 为准；通用仓库构建约定见 [开发说明](DEVELOPMENT.md)。

## 1. 适用范围与系统概览

本手册适用于以下工作：

- 为现有 Avalonia 插件加入 Vue/Vite 页面。
- 新建带 Web 页面的插件，并把 Vue 路由映射到启动器导航和菜单。
- 在浏览器模拟、WebView 开发和生产模式之间切换。
- 新增 IPC 方法、构建发布资源并排查路由、原生桥接、发现文件或进程问题。

当前系统由以下组件协作：

| 组件 | 职责 |
| --- | --- |
| 插件 `Web/` | Vue、Vue Router、TypeScript、浏览器模拟和 Vite 配置 |
| `@avalonia-template/vite-plugin-avalonia` | 设置插件 URL 基准路径、注入前端运行时引导脚本、把 Vite 限制在回环地址并发布开发发现文件 |
| `PluginWebAppService` | 校验 `plugin.json.web`，注册插件 Web 应用，选择 Production 或 WebView Dev，并解析路由 URI |
| `PluginStaticResourceService` | 在 Launcher 的回环 HTTP 服务下提供 Production 文件、History 回退和目录边界检查 |
| `PluginWebRouteViewModel` | 把插件 ID、页面 ID、标题和 History 路由转换为 `PluginWebRuntimeOptions` |
| `PluginWebViewPage` | 使用 `Avalonia.Controls.WebView.NativeWebView` 承载页面，维护当前文档信任、桥接就绪、导航状态和消息边界 |
| `PluginWebIpcService` | 按 `pluginKey + method` 注册和分发 .NET 处理器，并在插件卸载时清理 |
| `@avalonia-template/plugin-sdk` | 提供 `invoke()`、原生桥接调用、30 秒超时和浏览器模拟回退 |

插件加载时，`PluginLoader` 先根据清单向 `PluginWebAppService` 注册 Web 应用，再执行插件的 `RegisterAsync()` 注册 IPC 处理器。页面通过 `PluginWebRouteViewModel` 获取最终 URI，由 `PluginWebViewPage` 导航；只有当前可信文档才能进入对应插件的 IPC 处理器。

## 2. 环境准备

| 依赖 | 当前要求 |
| --- | --- |
| .NET SDK | .NET 10 |
| Node.js | `>=22.18.0` |
| pnpm | `11.15.1` |
| 前端工作区 | `frontend/packages/*` 和 `plugins/*/Web` |
| 插件 NuGet | `plugins/nuget.config` 指向仓库本地 `packages/nuget` 源 |

根 `package.json` 固定 `pnpm@11.15.1`，`.node-version` 内容为 `22`。版本管理器可以据此切换 Node 22，但仓库脚本不会安装或替换系统 Node。

全新克隆仓库后，先生成插件依赖的本地 NuGet 包，再按锁文件安装前端依赖。

Windows PowerShell：

```powershell
.\build.ps1 --build=nuget --configuration=Debug
pnpm install --frozen-lockfile
```

Linux/macOS：

```bash
./build.sh --build=nuget --configuration=Debug
pnpm install --frozen-lockfile
```

只有在有意更新依赖和 `pnpm-lock.yaml` 时才使用不带 `--frozen-lockfile` 的 `pnpm install`。新增到 `plugins/<项目名>/Web` 的包会被现有 `pnpm-workspace.yaml` 自动纳入工作区。

## 3. 三种运行模式

| 模式 | 页面资源 | IPC | 原生桥接 | HMR/工具 | 适用场景 |
| --- | --- | --- | --- | --- | --- |
| 生产模式 | Launcher 的回环静态资源服务 | 真实 .NET IPC | 有 | 无 HMR；无开发工具栏 | 发布前验证、最终运行 |
| WebView 开发模式 | 回环 Vite 开发服务器 | 真实 .NET IPC | 有 | Vite HMR、WebView 开发工具栏、受限控制台转发 | 前后端真实联调、.NET 断点 |
| 浏览器模拟模式 | 浏览器直接打开 Vite | 前端同数据结构模拟 | 没有 | 浏览器开发者工具、Vite HMR | 布局、路由、表单和前端错误态 |

`PluginWebResourceMode` 只有 `Production` 和 `WebViewDev`；浏览器模拟是浏览器前端工作流，不是宿主资源模式。

必须遵守以下边界：

- 只支持 History Router，不使用 Hash Router。
- Production 和 WebView Dev 使用真实宿主 IPC。
- 浏览器模拟模式没有原生桥接，也不会暴露 .NET 业务端点。
- Production 资源由 Launcher 在 `/plugins/{normalizedSegment}/` 下提供。
- Vite 只接受经过校验的回环 `http`/`https` 来源。

## 4. 快速体验 Template 插件

以下命令均从仓库根目录执行。首次运行先完成第 2 节的 NuGet 和 pnpm 准备。

### 4.1 浏览器模拟：先看前端

```powershell
pnpm --filter @avalonia-plugin/template dev
```

在浏览器打开 Vite 输出的回环 URL。此模式可访问 `/`、`/settings`、`/about`，并使用 `Web/src/mocks.ts` 提供的 `app.info`、`settings.get`、`settings.save` 模拟处理器。页面中不存在真实 .NET 原生桥接。

### 4.2 WebView Dev：一键真实联调

```powershell
pnpm dev:template
```

该命令会：

1. 用 `SkipPluginWebBuild=true` 构建 Template DLL、清单和开发占位页。
2. 启动 Template Vite 开发服务器，并等待属于本次租约的发现文件。
3. 启动 Launcher，设置 `AVALONIA_EXTRA_PLUGINS_PATH`，等待 Launcher 就绪握手。
4. 输出 `AVALONIA_WEB_DEV_READY <pid>`；此后 WebView 从 Vite 加载页面并调用真实 IPC。

结束时在同一终端按 `Ctrl+C`，让编排脚本清理它拥有的 Launcher/Vite 进程树以及匹配的发现/就绪文件。

### 4.3 Production：验证构建资源

```powershell
dotnet build plugins/Avalonia.Plugin.Template/Avalonia.Plugin.Template.csproj -c Debug
$env:AVALONIA_EXTRA_PLUGINS_PATH = (Resolve-Path "plugins/Avalonia.Plugin.Template/bin/Debug/net10.0").Path
dotnet run --project src/launcher/Avalonia.Launcher.Desktop
```

默认 Template 构建会运行 `pnpm build`，把真实 `Web/dist/**` 复制到输出 `web/**`。在 Launcher 中依次打开 Web 首页、Web 设置、Web 关于，检查三个 History 路由和真实 IPC。结束 Launcher 后可清理当前终端变量：

```powershell
$env:AVALONIA_EXTRA_PLUGINS_PATH = $null
```

## 5. 为插件接入 Web 前端

以下示例用 `Avalonia.Plugin.Example` 和 `EXAMPLE-PLUGIN-ID` 表示新插件。插件 ID 必须在 `.csproj`、Vite 配置和 C# 元数据中保持一致。

### 5.1 推荐目录

```text
plugins/Avalonia.Plugin.Example/
  Avalonia.Plugin.Example.csproj
  ExamplePlugin.cs
  ViewModels/
    ExampleWebViewModel.cs
    ExampleSettingsWebViewModel.cs
    ExampleAboutWebViewModel.cs
  Web/
    package.json
    tsconfig.json
    vite.config.ts
    index.html
    dev-placeholder.html
    src/
      main.ts
      router.ts
      mocks.ts
      App.vue
      views/
```

`Web/dist/` 是 Vite 构建产物；插件输出使用小写 `web/`，与清单的 `root` 一致。

### 5.2 声明 Web 清单与构建复制规则

共享清单模型为 `PluginManifest.Web: PluginWebManifest?`，实际字段只有资源根和入口：

```csharp
var web = new PluginWebManifest
{
    Root = "web",
    Entry = "index.html"
};
```

正常插件项目不需要手写 `plugin.json`。在 `.csproj` 设置 `PluginWebRoot` 和 `PluginWebEntry` 后，共享 MSBuild 目标会生成相应的 `web` 节点。下面的构建、复制和发布接入与 Template 当前实现一致：

```xml
<PropertyGroup>
  <PluginWebRoot>web</PluginWebRoot>
  <PluginWebEntry>index.html</PluginWebEntry>
  <PluginWebSourceDirectory>$(MSBuildProjectDirectory)\Web</PluginWebSourceDirectory>
  <PluginWebDistDirectory>$(PluginWebSourceDirectory)\dist</PluginWebDistDirectory>
</PropertyGroup>

<Target Name="BuildPluginWeb" BeforeTargets="Build" Condition="'$(SkipPluginWebBuild)' != 'true'">
  <Exec WorkingDirectory="$(PluginWebSourceDirectory)" Command="pnpm build" />
</Target>

<Target Name="CopyPluginWebToOutput" AfterTargets="Build" Condition="'$(SkipPluginWebBuild)' != 'true'">
  <Error Condition="!Exists('$(PluginWebDistDirectory)\index.html')"
         Text="Plugin Web build output is missing. Run pnpm build in $(PluginWebSourceDirectory)." />
  <ItemGroup>
    <_PluginWebDistFiles Include="$(PluginWebDistDirectory)\**\*" />
  </ItemGroup>
  <MakeDir Directories="$(OutDir)$(PluginWebRoot)" />
  <Copy SourceFiles="@(_PluginWebDistFiles)"
        DestinationFiles="@(_PluginWebDistFiles->'$(OutDir)$(PluginWebRoot)\%(RecursiveDir)%(Filename)%(Extension)')"
        SkipUnchangedFiles="true" />
</Target>

<Target Name="CopyPluginWebDevPlaceholderToOutput" AfterTargets="Build" Condition="'$(SkipPluginWebBuild)' == 'true'">
  <MakeDir Directories="$(OutDir)$(PluginWebRoot)" />
  <Copy SourceFiles="$(PluginWebSourceDirectory)\dev-placeholder.html"
        DestinationFiles="$(OutDir)$(PluginWebRoot)\index.html" />
</Target>

<Target Name="ValidatePluginWebForPublish" BeforeTargets="Publish">
  <Error Condition="!Exists('$(PluginWebDistDirectory)\index.html')"
         Text="Plugin Web distribution is missing. Build the Web app before publishing." />
</Target>

<Target Name="CopyPluginWebToPublishOutput" AfterTargets="Publish">
  <ItemGroup>
    <_PublishedPluginWebDistFiles Include="$(PluginWebDistDirectory)\**\*" />
  </ItemGroup>
  <MakeDir Directories="$(PublishDir)$(PluginWebRoot)" />
  <Copy SourceFiles="@(_PublishedPluginWebDistFiles)"
        DestinationFiles="@(_PublishedPluginWebDistFiles->'$(PublishDir)$(PluginWebRoot)\%(RecursiveDir)%(Filename)%(Extension)')"
        SkipUnchangedFiles="true" />
</Target>
```

其中只有 `PluginWebRoot` 和 `PluginWebEntry` 驱动清单；其余属性和目标是插件项目自己的构建约定。使用跳过 Web 构建流程时必须提供安全的 `dev-placeholder.html`，发布仍必须使用真实 `dist/index.html`，不能发布占位页。

生成的 `plugin.json` 应包含：

```json
{
  "pluginId": "EXAMPLE-PLUGIN-ID",
  "web": {
    "root": "web",
    "entry": "index.html"
  }
}
```

最终发布目录至少包含：

```text
plugin.json
Avalonia.Plugin.Example.dll
web/
  index.html
  assets/
    ...
```

`root` 和 `entry` 必须是非空、可移植的相对路径。宿主拒绝绝对路径、盘符、`..` 越界、缺失目录/入口以及符号链接/重解析点路径。

### 5.3 配置 Vite

新插件的 `Web/package.json` 应依赖工作区 SDK，并把 Vite 插件放在开发依赖中。可直接参考 `plugins/Avalonia.Plugin.Template/Web/package.json`；`plugins/*/Web` 已在根工作区中。

`vite.config.ts`：

```ts
import { fileURLToPath, URL } from "node:url";
import vue from "@vitejs/plugin-vue";
import { avaloniaPlugin } from "@avalonia-template/vite-plugin-avalonia";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [
    vue(),
    ...avaloniaPlugin({
      pluginId: "EXAMPLE-PLUGIN-ID",
      pluginOutputDirectory: fileURLToPath(new URL("../bin/Debug/net10.0/", import.meta.url)),
    }),
  ],
});
```

Vite 插件根据与宿主相同的规则生成 `/plugins/{normalizedSegment}/` 基准路径，并设置 `server.host = "127.0.0.1"`、`strictPort = false`。不要改为远程主机。当前根编排脚本只内建 `template` 别名；新插件若需要一键 WebView 开发命令，还要在 `frontend/tools/run-plugin-dev.mjs` 和根 `package.json` 中增加对应计划/脚本。

### 5.4 创建宿主路由页面

每个需要出现在 Avalonia 导航中的路由建立一个 `PluginWebRouteViewModel`，并映射到共享 `PluginWebViewPage`：

```csharp
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.Shared.ViewModels;

namespace Avalonia.Plugin.Example.ViewModels;

[NavigationItem("ExampleSettingsWeb")]
[Menu("Web Settings", "ExampleSettingsWeb", Status = "Web", Order = 2, IconName = "Settings")]
[ViewMap(typeof(global::Avalonia.Plugin.Shared.Components.PluginWebViewPage))]
public sealed partial class ExampleSettingsWebViewModel : PluginWebRouteViewModel
{
    public ExampleSettingsWebViewModel()
        : base(
            ExamplePlugin.Id,
            "example-settings",
            "Example Settings",
            "Edit settings through Web IPC.",
            "/settings")
    {
    }
}
```

不要在 ViewModel 中拼 Vite 端口或 Production URL。`PluginWebRouteViewModel` 会通过已注册的 `IPluginWebAppService` 解析资源模式、路由、允许来源和最终 URI。

## 6. 配置 History 多路由

当前只支持 Vue Router 的 History 模式：

```ts
import { createRouter, createWebHistory } from "vue-router";
import AboutView from "./views/AboutView.vue";
import HomeView from "./views/HomeView.vue";
import SettingsView from "./views/SettingsView.vue";

export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: "/", component: HomeView },
    { path: "/settings", component: SettingsView },
    { path: "/about", component: AboutView },
  ],
});
```

必须使用 `import.meta.env.BASE_URL`，使浏览器模拟、WebView 开发和生产模式都落在 Vite 插件生成的插件前缀下。不要使用 `createWebHashHistory`，也不要硬编码 `/` 作为部署基准路径。

Template 的同步关系如下：

| Vue 路由 | C# ViewModel | Avalonia 导航/菜单 |
| --- | --- | --- |
| `/` | `TemplateWebViewModel` | `TemplateWeb` / Web Home |
| `/settings` | `TemplateSettingsWebViewModel` | `TemplateSettingsWeb` / Web Settings |
| `/about` | `TemplateAboutWebViewModel` | `TemplateAboutWeb` / Web About |

Vue 内部的 `RouterLink` 在当前 WebView 内切换路由；Avalonia 菜单则创建相应的路由 ViewModel。两边不会自动生成彼此的配置，因此新增路由时必须同时更新 Vue Router、C# ViewModel、`[NavigationItem]`、`[Menu]` 和 `[ViewMap]`。

生产模式对不存在且最后一段没有扩展名的路径回退到清单 `entry`。因此刷新 `/settings` 或直接打开 `/about` 会返回 `index.html`，再由 Vue Router 渲染；缺失的 `app.js`、`.css`、图片等资源不会回退。

## 7. 静态资源、URL 命名空间与安全边界

生产模式为每个插件注册以下回环 URL 命名空间：

```text
http://127.0.0.1:<random-port>/plugins/{normalizedSegment}/
```

`normalizedSegment` 从 `pluginId` 生成：保留字母、数字、`.`、`_`、`-`，连续的其他字符折叠为 `-`，去掉首尾的 `-`、`_`、`.`；结果为空或与其他插件冲突时拒绝注册。

请求处理规则：

| 请求 | 结果 |
| --- | --- |
| 已存在的文件 | 返回真实文件，优先于回退 |
| 空路径或缺失的无扩展名路径 | 返回清单 `entry`，支持 History 路由 |
| 缺失且最后一段有扩展名 | `404` |
| 解码后越界、编码分隔符、反斜杠逃逸 | `404` |
| 未注册的插件 URL 前缀 | `404` |
| 已注册插件前缀下存在的公开文件 | 可由同一 Launcher 来源的 HTTP 请求读取；静态服务不校验发起页面属于哪个插件 |
| 符号链接/重解析点或打开后发现目录外目标 | 拒绝访问 |

静态资源注册把每个 URL 前缀映射到对应插件的 Web 根目录，并阻止路径越界；它隔离的是 URL 到文件目录的映射，不是 HTTP 请求来源。只要插件前缀已注册且文件存在，同一 Launcher 来源中的其他页面或脚本也可以请求该公开资源。不要把静态资源 URL 当成机密数据访问控制；敏感数据应通过带业务授权的 .NET 服务提供。

真正按当前插件限制的是 `PluginWebViewPage` 的顶层导航授权和 IPC 边界：顶层文档必须位于当前页面的授权基准 URI 与允许来源内，IPC 的 `pluginKey`、可信文档 URI 和文档 URL 命名空间也必须与当前页面一致。这些限制不会让普通静态 HTTP 请求自动获得请求方插件身份。

URL 路径和 IPC 边界也不等于浏览器存储拥有独立来源。宿主不保证每个插件获得独立的 `localStorage`、Cookie 或 IndexedDB 来源；尤其在 Production 中，多个插件路径位于同一 Launcher 协议、主机和端口下。应使用带插件 ID 前缀的存储键，或把敏感/权威数据存放在 .NET 服务中。Cookie 还必须自行设置合适的路径和安全属性。

## 8. 使用 .NET IPC

本节先给出前端调用、C# 处理器和浏览器模拟的完整接入方式，再说明宿主放行真实 IPC 前执行的安全检查。

### 8.1 TypeScript 调用

Vite 插件注入的引导脚本负责安装前端运行时；业务代码使用 SDK 的 `invoke()`：

```ts
import { invoke } from "@avalonia-template/plugin-sdk";

interface TemplateSettings {
  displayName: string;
  refreshInterval: number;
  notificationsEnabled: boolean;
}

const current = await invoke<TemplateSettings>("settings.get");
const saved = await invoke<TemplateSettings>("settings.save", {
  displayName: current.displayName,
  refreshInterval: 60,
  notificationsEnabled: true,
});
```

`invoke()` 在原生桥接可用时走真实 IPC，否则查找已注册模拟处理器；没有桥接且没有同名模拟处理器时返回 `bridge_unavailable`。完整请求/响应字段、超时和内建错误码见 [IPC 请求与响应](WEBVIEW_IPC.md#ipc-请求与响应)。

### 8.2 注册 C# 处理器

在插件 `RegisterAsync(IServiceProvider serviceProvider)` 中注册处理器：

```csharp
using System.Text.Json;
using Avalonia.Plugin.Shared.Services;

if (serviceProvider.GetService(typeof(IPluginWebIpcService)) is IPluginWebIpcService ipc)
{
    ipc.RegisterHandler(new PluginWebIpcHandlerRegistration(
        PluginId,
        "profile.save",
        (request, _) =>
        {
            if (request.Payload.ValueKind != JsonValueKind.Object
                || !request.Payload.TryGetProperty("displayName", out var nameElement)
                || nameElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(nameElement.GetString()))
            {
                return ValueTask.FromResult(PluginWebIpcResponse.Failure(
                    request,
                    "invalid_payload",
                    "displayName is required."));
            }

            return ValueTask.FromResult(PluginWebIpcResponse.Success(request, new
            {
                displayName = nameElement.GetString()!.Trim()
            }));
        }));
}
```

处理器必须校验载荷，不要信任前端类型。`PluginWebIpcService` 以 `pluginKey + method` 为命名空间；不同插件可以使用相同方法名，但不能借此访问对方处理器。插件禁用、卸载或注册失败时，宿主会清理该插件的 Web 和 IPC 注册。

### 8.3 提供同数据结构的浏览器模拟

浏览器模拟不伪造原生桥接，只注册与 C# 相同的方法和返回数据结构。`mocks.ts` 仅导出注册函数，模块导入时不自动注册：

```ts
import { registerAvaloniaPluginMocks } from "@avalonia-template/plugin-sdk";

interface Profile {
  displayName: string;
}

let profile: Profile = { displayName: "Browser User" };

export function registerBrowserMocks(): void {
  registerAvaloniaPluginMocks({
    "profile.save": payload => {
      if (!payload || typeof payload !== "object" || Array.isArray(payload)) {
        throw { code: "invalid_payload", message: "Profile payload must be an object." };
      }

      const displayName = typeof (payload as Partial<Profile>).displayName === "string"
        ? (payload as Partial<Profile>).displayName!.trim()
        : "";
      if (!displayName) {
        throw { code: "invalid_payload", message: "displayName is required." };
      }

      profile = { displayName };
      return { ...profile };
    },
  });
}
```

`main.ts` 在页面启动时判断原生桥接是否存在，只为没有原生桥接的浏览器模拟页面注册模拟处理器：

```ts
import { isNativeBridgeAvailable } from "@avalonia-template/plugin-sdk";
import { registerBrowserMocks } from "./mocks";

if (!isNativeBridgeAvailable()) {
  registerBrowserMocks();
}
```

生产模式和 WebView 开发模式必须使用真实 IPC。上述判断只用于启动时区分浏览器页面与原生 WebView；不要在 `invoke()` 返回 `bridge_unavailable`、超时或其他桥接错误后再注册模拟处理器，否则会掩盖真实桥接故障。模拟处理器用于前端开发便利，不是权限校验或后端实现，也不要放入生产凭据、现场数据或能够绕过服务端规则的秘密。

### 8.4 桥接就绪与安全门

真实 IPC 同时受以下条件约束：

- 运行时的 `pluginKey` 必须与当前 `PluginWebRouteViewModel` 页面身份一致。
- 当前导航必须成功完成，当前文档 URI 必须仍是当前上下文令牌对应的可信文档。
- 文档来源必须在页面的 `AllowedOrigins` 中；该集合包含 Production 来源，以及通过校验时的 WebView Dev 来源。
- 当前 URI 必须位于该页面授权的插件 URL 命名空间下。
- 桥接必须启用并由宿主标记为就绪；SDK 会在就绪前暂存原生请求，并受 30 秒超时限制。
- 导航、ViewModel 或 WebView 已变化时，旧上下文的消息和异步响应不会投递到新文档。

这些检查不能替代处理器的业务授权和载荷校验。完整协议、安全状态机和生命周期说明见 [Web 插件宿主与 IPC 约定](WEBVIEW_IPC.md)。

## 9. 推荐调试工作流

按以下顺序调试最容易定位问题：

1. **浏览器模拟**：运行插件包的 `dev`，先确认 Vue 页面、History 路由、表单、类型和模拟错误态。
2. **WebView 开发模式**：运行 `pnpm dev:template`，确认真实 IPC、允许来源、导航、.NET 断点和 WebView 行为。
3. **生产模式**：默认构建插件并由 Launcher 加载输出目录，确认静态资源、深路由回退和最终包布局。

### HMR 与 WebView 工具栏

- 浏览器模拟和 WebView 开发模式都由 Vite 提供 HMR。
- WebView 开发模式显示后退、前进、刷新、资源模式、当前路由和导航状态。
- Vue/样式修改通常可 HMR；C#、清单、插件生命周期或处理器注册修改需要重建并重启相应进程。
- 生产模式没有 HMR、开发工具栏或控制台转发。

### VS Code

选择 `.vscode/launch.json` 中的 `Web Plugin: Template (orchestrated)`。它启动后台任务 `dev-plugin-template-web`；任务收到 `AVALONIA_WEB_DEV_READY <pid>` 后才就绪。随后在进程选择器中选择 PID 与输出完全一致的 Avalonia Launcher 进程。

停止附加调试可能只断开调试器。调试结束后还要停止后台任务，或在其终端中按 `Ctrl+C`。

### 发现文件、租约与清理

Vite 插件把 `.avalonia-web-dev.json` 写到插件输出目录，内容包括 `pluginId`、`origin`、`processId`、`startedAt` 和 `leaseId`。宿主读取前四项，校验插件身份、回环 `http`/`https` 来源、端口、PID、进程启动时间和来源健康状态；无效、陈旧或不可达记录会被忽略并回到生产模式。

Vite 插件和编排脚本使用 `leaseId`、claim/tmp 文件以及 Launcher 就绪文件判断所有权。优先正常 `Ctrl+C`，只清理当前运行拥有的文件和进程；不要手删其他实例的发现、claim、tmp 或就绪文件。

### 日志

- `PluginWebAppService` 会记录发现文件缺失、无效、陈旧或来源不健康等信息。
- `PluginWebIpcService` 记录插件、方法、请求 ID、耗时、结果码和成功状态，不记录请求载荷。
- `PluginWebViewPage` 记录被拒绝的文档/消息原因；WebView Dev 的控制台转发有长度和速率限制，不能作为业务日志通道。
- 排障时同时查看编排脚本终端、Vite 输出和 Launcher 日志。

## 10. 新增路由和 IPC 方法

新增功能时需要同步修改前端、插件 C# 入口和维护文档；以下清单用于避免只改一侧造成路由或调用不一致。

### 新增路由清单

1. 在 `Web/src/views/` 添加 Vue 视图。
2. 在 `Web/src/router.ts` 添加以 `/` 开头的 History 路由。
3. 保持 `createWebHistory(import.meta.env.BASE_URL)`，不要改 Hash Router 或硬编码基准路径。
4. 添加继承 `PluginWebRouteViewModel` 的 C# ViewModel，路由字符串与 Vue 保持一致。
5. 添加或更新 `[NavigationItem]`、`[Menu]`、`ParentKey`、`Order` 和 `[ViewMap(typeof(PluginWebViewPage))]`。
6. 在浏览器模拟模式验证内部导航，在 WebView 开发模式验证菜单入口，在生产模式验证直接打开/刷新深路由。
7. 确认真实缺失资源仍返回 `404`，没有被错误地当成路由回退。

### 新增 IPC 方法清单

1. 在插件 `RegisterAsync()` 注册 `PluginWebIpcHandlerRegistration`。
2. 使用当前插件 ID，选择稳定且不空白的方法名。
3. 在 C# 处理器中校验载荷、权限和业务约束，并返回结构化业务错误。
4. 在 TypeScript 中定义请求/响应类型并通过 `invoke<T>()` 调用。
5. 在浏览器模拟模式注册相同方法、返回数据结构和关键错误语义。
6. 同步更新页面、示例和维护文档。
7. 分别在浏览器模拟与 WebView 开发/生产模式验证模拟处理器和真实处理器。

## 11. 构建、打包与发布

前端完整验证：

```powershell
pnpm install --frozen-lockfile
pnpm typecheck
pnpm build
```

单插件构建：

```powershell
dotnet build plugins/Avalonia.Plugin.Template/Avalonia.Plugin.Template.csproj --configuration Debug
```

Template 默认构建会运行其 `pnpm build` 并复制真实分发文件。`SkipPluginWebBuild=true` 只用于开发编排脚本；发布即使跳过 Web 构建，也要求已有真实 `Web/dist/index.html`。

使用 Cake 发布并打包所有插件：

```powershell
.\build.ps1 --build=plugin --configuration=Debug
```

Linux/macOS：

```bash
./build.sh --build=plugin --configuration=Debug
```

发布目录位于 `packages/plugins/<PluginProjectName>/publish`，ZIP 位于 `packages/plugins/zip/<PluginProjectName>-<PluginVersion>.zip`。Template ZIP 预期包含：

```text
plugin.json
Avalonia.Plugin.Template.dll
web/
  index.html
  assets/
    ...
```

打包会排除 `.pdb`、`.xml`、`.runtimeconfig.json` 等非交付文件。不要手工把 `bin` 目录压缩成插件包，也不要把开发占位页当作 Production 入口。

## 12. 常见问题

| 现象 | 检查与处理 |
| --- | --- |
| Node 引擎错误 | 使用 Node `>=22.18.0`；`.node-version` 只提示版本，不会修改系统 Node |
| 插件还原找不到 Shared/Generators | 先运行 `build.ps1/build.sh --build=nuget` 生成本地 NuGet 包 |
| 包导出或 SDK/Vite 插件分发目录缺失 | 使用 Template 的 `predev`/`prebuild`，或先运行工作区 `pnpm build` |
| 缺少 `web/index.html` | 检查 `PluginWebRoot`、`PluginWebEntry`、Vite 分发文件和复制目标；发布必须使用真实分发文件 |
| `/settings` 或 `/about` 在 Production 返回 404 | 使用 `createWebHistory(import.meta.env.BASE_URL)`；确认路由最后一段无扩展名且清单入口存在 |
| 缺失 `.js`/`.css` 返回 404 | 这是预期行为；修复基准路径、构建文件名或复制路径，不要要求 History 回退返回 HTML |
| 发现文件无效或陈旧 | 确认 `pluginId`、PID、`startedAt`、租约和 Vite 来源属于本次运行；正常退出后重试 |
| Vite 来源被拒绝 | 只使用带有效端口的回环 `http`/`https`，不能有凭据、额外路径、查询或片段 |
| `bridge_unavailable` | 浏览器模式应注册模拟处理器；WebView 中检查运行时是否安装、当前文档是否可信且桥接已就绪 |
| `plugin_mismatch` | 检查 `.csproj`、Vite `pluginId`、C# `PluginId` 和当前路由 ViewModel 是否一致 |
| `method_not_found` | 检查处理器是否在当前插件 `RegisterAsync()` 注册，方法拼写是否与前端一致 |
| HMR 不生效 | 确认当前是浏览器模拟/WebView 开发模式、页面来自 Vite，且 Vite/租约未失效；生产模式不支持 HMR |
| VS Code 附加调试后进程仍在 | 停止 `dev-plugin-template-web` 后台任务，或在编排脚本终端按 `Ctrl+C` |
| 停止 Vite 后仍显示开发页面 | 宿主在插件注册时选定资源模式；关闭开发进程、清理匹配的发现文件、重建 Production 并重启 Launcher |
| 插件间 localStorage/IndexedDB 数据冲突 | 路径命名空间不是独立来源；使用插件 ID 前缀或改用 .NET 持久化服务 |
| 把已注册插件的静态 URL 当成访问控制 | 静态服务只校验已注册前缀和文件目录边界，不识别请求方插件；机密数据应由带授权的 IPC/.NET 服务返回 |

## 13. 安全与维护检查表

- [ ] Vite 只监听回环地址，未把开发服务器暴露到局域网或公网。
- [ ] `.csproj`、C# 元数据、Vite 配置中的插件 ID 完全一致。
- [ ] `PluginWebRoot`/`PluginWebEntry` 为相对路径，发布目录不包含符号链接/重解析点 Web 根。
- [ ] Vue Router 使用 `createWebHistory(import.meta.env.BASE_URL)`，没有 Hash Router 或硬编码基准路径。
- [ ] 已注册前缀下的静态文件只包含可公开读取的前端资源，不把静态 URL 当作请求方身份或机密数据访问控制。
- [ ] WebView 顶层导航授权和 IPC 身份/可信文档 URL 命名空间均限制在当前插件范围内。
- [ ] 每个 IPC 处理器校验载荷、权限、范围和业务约束，不信任浏览器模拟。
- [ ] 日志不记录 IPC 载荷、凭据、Cookie、令牌或敏感业务数据。
- [ ] 浏览器模拟不包含生产密钥、真实账号、现场数据或绕过服务端授权的逻辑。
- [ ] 前端存储键包含插件 ID 前缀；敏感和权威数据保存在 .NET 服务中。
- [ ] SDK/运行时升级后重新构建 Web 包，并验证桥接就绪和导航切换。
- [ ] 禁用/卸载后的 Web、IPC 和进程资源能够清理；重新启用后需要重启 Launcher，不依赖热启。
- [ ] 发布前执行冻结锁文件安装、类型检查、构建、单插件构建和 Cake 插件打包检查。

## 14. 延伸阅读

- [Web 插件宿主与 IPC 约定](WEBVIEW_IPC.md)：协议字段、错误码、信任边界、安全状态机和生命周期技术参考。
- [开发说明](DEVELOPMENT.md)：仓库构建、Launcher 调试、插件生命周期和通用维护约定。
- [Template 插件 README](../plugins/Avalonia.Plugin.Template/README.md)：当前参考实现的功能入口和插件特定说明。
