# Web 插件宿主与 IPC 约定

本文档作为当前 Web 插件宿主的协议、安全和生命周期技术参考，记录运行模型、静态资源路由、IPC 字段与信任边界。面向开发者的完整操作流程见 [Web 插件使用手册](WEB_PLUGIN_GUIDE.md)，仓库构建约定见 [开发说明](DEVELOPMENT.md)。

## 清单与发布目录

Web 插件通过 `plugin.json` 的可选 `web` 节点声明资源根目录和入口文件：

```json
{
  "pluginId": "TEMPLATE-PLUGIN-0000-0000-000000000001",
  "web": {
    "root": "web",
    "entry": "index.html"
  }
}
```

插件项目用以下 MSBuild 属性生成该节点：

```xml
<PluginWebRoot>web</PluginWebRoot>
<PluginWebEntry>index.html</PluginWebEntry>
```

`root` 和 `entry` 都是可移植相对路径，不能包含盘符、根路径、空路径段或 `.`/`..`。Template 的最终布局为：

```text
plugin.json
Avalonia.Plugin.Template.dll
web/
  index.html
  assets/
    ...
```

宿主从插件安装目录解析 `web/root`，再从该目录解析 `web/entry`；目录或入口缺失、越界，或者路径经过符号链接/重解析点时，Web 应用注册失败。

## 生产模式静态资源宿主

每个插件拥有隔离的本机回环 URL 前缀：

```text
http://127.0.0.1:<random-port>/plugins/{normalizedSegment}/
```

`normalizedSegment` 由 `pluginId` 规范化而来，只保留字母、数字、`.`、`_`、`-`，连续的其他字符折叠为 `-`，并拒绝规范化后为空或与其他插件冲突的路径段。

文件解析规则：

- 只服务 `GET` 和 `HEAD`；每个注册目录只允许访问自己的 `web` 根目录。
- 先查找请求对应的真实文件。真实文件存在时始终优先，不会被 SPA 后备覆盖。
- 只有文件不存在，且请求为空路径或最后一个路径段没有扩展名时，才回退到清单的 `entry`。
- 缺失且最后一段带扩展名（例如 `assets/missing.js`）时返回 `404`。
- 解码后的目录穿越、编码穿越、反斜杠逃逸、符号链接/重解析点和打开文件后发现的目录外目标都会被拒绝。

这套后备机制专门支持 History 路由。前端应使用 `createWebHistory(import.meta.env.BASE_URL)`；当前实现不是 Hash 路由方案。

## 页面与路由

宿主页面是 `Avalonia.Controls.WebView.NativeWebView` 驱动的 `PluginWebViewPage`。插件导航 ViewModel 继承 `PluginWebRouteViewModel`，由 `IPluginWebAppService` 解析当前资源模式、允许来源和最终 URI。

Template 演示三个 History 路由，共用同一份 Vue 构建产物：

| 视图模型 | 路由 | 用途 |
| --- | --- | --- |
| `TemplateWebViewModel` | `/` | `app.info` |
| `TemplateSettingsWebViewModel` | `/settings` | `settings.get` / `settings.save` |
| `TemplateAboutWebViewModel` | `/about` | 运行时说明 |

最小 C# 路由页面：

```csharp
[NavigationItem("ExampleWeb")]
[Menu("Example Web", "ExampleWeb", Status = "Web", Order = 1, IconName = "Globe")]
[ViewMap(typeof(global::Avalonia.Plugin.Shared.Components.PluginWebViewPage))]
public sealed partial class ExampleWebViewModel : PluginWebRouteViewModel
{
    public ExampleWebViewModel()
        : base(
            ExamplePlugin.Id,
            "example-home",
            "Example Web",
            "Example Web plugin page.",
            "/")
    {
    }
}
```

生产模式页面地址始终来自宿主静态资源服务，不应硬编码开发服务器端口。

## 三种运行模式

| 模式 | 页面资源 | IPC | 工具栏/HMR | 控制台转发 |
| --- | --- | --- | --- | --- |
| 生产模式 | 宿主本机回环静态资源 | 真实 .NET IPC | 无 | 关闭 |
| WebView 开发模式 | 本机回环 Vite 开发服务器 | 真实 .NET IPC | WebView 开发工具栏与 HMR | 开启，受限流保护 |
| 浏览器模拟模式 | 浏览器直接打开 Vite | 前端模拟 IPC | 浏览器开发者工具与 HMR | 不转发到 .NET |

文档中的“WebView 开发模式/开发资源模式”指宿主发现有效 `.avalonia-web-dev.json` 后，`PluginWebRouteViewModel.IsDevelopmentMode` 为 `true`。Vite 的 `import.meta.env.DEV` 则在 WebView 开发模式和浏览器模拟模式中都为 `true`；二者不能混为一谈。浏览器模拟模式没有 `.NET` 桥接，也没有宿主业务端点。

推荐流程是先用浏览器模拟模式调试布局和前端状态，再用 WebView 开发模式验证真实 IPC、来源信任、导航和 .NET 断点，最后用生产构建确认发布资源。

## IPC 请求与响应

前端 SDK 的调用入口：

```ts
import { invoke } from "@avalonia-template/plugin-sdk";

const info = await invoke<AppInfo>("app.info");
const settings = await invoke<TemplateSettings>("settings.get");
const saved = await invoke<TemplateSettings>("settings.save", {
  displayName: "Template User",
  refreshInterval: 30,
  notificationsEnabled: true,
});
```

真实 IPC 请求的 JSON 字段完全如下：

```json
{
  "kind": "avalonia-plugin-ipc-request",
  "id": "avalonia-<unique-id>",
  "pluginKey": "TEMPLATE-PLUGIN-0000-0000-000000000001",
  "method": "settings.get",
  "payload": null
}
```

成功响应：

```json
{
  "kind": "avalonia-plugin-ipc-response",
  "id": "avalonia-<unique-id>",
  "pluginKey": "TEMPLATE-PLUGIN-0000-0000-000000000001",
  "method": "settings.get",
  "ok": true,
  "payload": {
    "displayName": "Template User",
    "refreshInterval": 30,
    "notificationsEnabled": true
  },
  "error": null
}
```

失败响应的完整信封结构为：

```json
{
  "kind": "avalonia-plugin-ipc-response",
  "id": "avalonia-<unique-id>",
  "pluginKey": "TEMPLATE-PLUGIN-0000-0000-000000000001",
  "method": "settings.save",
  "ok": false,
  "payload": null,
  "error": {
    "code": "invalid_payload",
    "message": "refreshInterval must be an integer from 5 to 3600.",
    "details": null
  }
}
```

`details` 是可选的附加数据；SDK 解析失败响应时至少要求 `kind/id/pluginKey/method/ok` 有效，且 `error.code` 为字符串。缺少 `error.code` 的格式错误失败信封会被 `parseResponse` 忽略，待处理请求不会立即收到 `ipc_error`，而是在 30 秒后以 `timeout` 失败。对格式有效的响应，SDK 还会核对 `id`、`pluginKey` 和 `method`。

### 内建与示例错误码

| code | 来源 | 含义 |
| --- | --- | --- |
| `invalid_method` | 前端 SDK | `invoke()` 的方法为空或只有空白 |
| `bridge_unavailable` | 前端 SDK / 旧版运行时辅助程序 | 原生桥不存在且没有对应模拟；原生桥已存在但运行时配置未成功安装时，原生分支优先并直接失败、不回退模拟；运行时配置已安装但 `bridgeEnabled=false` 时原生桥被视为不可用，有对应模拟就调用模拟，无模拟才返回此错误 |
| `bridge_send_failed` | 前端 SDK | 桥接存在，但发送原生消息时抛出异常 |
| `timeout` | 前端 SDK / 旧版运行时辅助程序 | 30 秒内没有收到匹配响应 |
| `bridge_disabled` | `PluginWebViewPage` | 当前页面的宿主运行时选项禁用了桥接 |
| `invalid_request` | `PluginWebViewPage` | 请求缺少 `id/pluginKey/method`，或请求 JSON 无法反序列化为有效请求 |
| `plugin_mismatch` | `PluginWebViewPage` | 请求/运行时的 `pluginKey` 与当前页面插件不一致 |
| `origin_not_allowed` | `PluginWebViewPage` | 当前可信文档来源不在页面允许来源中 |
| `service_unavailable` | `PluginWebViewPage` | 宿主无法取得 `IPluginWebIpcService` |
| `ipc_error` | `PluginWebViewPage` | 宿主在处理或分发请求时发生未分类内部异常，并返回带明确 `ipc_error` 错误码的失败响应 |
| `method_not_found` | `PluginWebIpcService` | 当前 `pluginKey + method` 没有已注册处理器 |
| `cancelled` | `PluginWebIpcService` | 调用使用的 cancellation token 已取消 |
| `handler_error` | `PluginWebIpcService` | 处理器抛出未处理异常 |
| `invalid_payload` | Template C# 处理器 / 浏览器模拟 | `settings.save` 负载不符合 Template 校验规则 |

插件处理器还可以定义自己的业务错误码；它们不是宿主内建错误码。接入与排障中的对应关系见 [Web 插件使用手册](WEB_PLUGIN_GUIDE.md#12-常见问题)。

## 注册 .NET 方法

插件在 `RegisterAsync(IServiceProvider)` 中按 `pluginKey + method` 注册处理器：

```csharp
if (serviceProvider.GetService(typeof(IPluginWebIpcService)) is IPluginWebIpcService ipc)
{
    ipc.RegisterHandler(new PluginWebIpcHandlerRegistration(
        PluginId,
        "app.info",
        (request, _) => ValueTask.FromResult(
            PluginWebIpcResponse.Success(request, new
            {
                pluginId = PluginId,
                version = Version,
                runtime = RuntimeInformation.FrameworkDescription
            }))));
}
```

未注册方法返回 `method_not_found`；处理器抛出异常返回 `handler_error`。插件被注销、禁用或卸载时，宿主会移除该插件的所有 IPC 处理器。

### 兼容的 HTTP/SSE 页面

`IPluginWebDataService` 及其 JSON/HTTP 端点、SSE 事件流仍然存在，ScottPlot 等旧页面仍在使用。通过旧 `PluginWebStaticResourceOptions` 流程注册的 HTML 也仍可获得 `window.__avaloniaPluginApi` 和 `window.__avaloniaPluginEvents`。这些是兼容能力，不是清单驱动 Template Web 应用的默认业务通道；新双向业务调用优先注册 IPC，只有确实需要服务端单向推送时才使用 SSE。

## Template 的真实契约

| 方法 | 负载 | 返回值 |
| --- | --- | --- |
| `app.info` | `null` | `{ pluginId, version, runtime }` |
| `settings.get` | `null` | `{ displayName, refreshInterval, notificationsEnabled }` |
| `settings.save` | 同上设置对象 | 保存后的同数据结构设置对象 |

`settings.save` 的 C# 处理器和浏览器模拟使用相同规则：`displayName` 去除首尾空白后为 1–100 个字符，`refreshInterval` 必须是 5–3600 的整数，`notificationsEnabled` 必须是布尔值；失败码为 `invalid_payload`。示例设置仅保存在进程内存中。

## 信任边界与限制

生产模式和 WebView 开发模式都使用真实 IPC；浏览器模拟模式只调用前端注册的模拟处理器。真实 IPC 只有在以下条件同时成立时才会分发：

- 请求来自当前 `PluginWebViewPage` 的当前 WebView 实例。
- 导航已成功完成，当前文档 URI 与当前导航上下文令牌的可信快照仍然一致。
- 当前文档来源在该页面的 `AllowedOrigins` 中。
- 当前文档 URI 位于该插件运行时的精确 `AuthorizedBaseUri` 路径命名空间内；兄弟插件、相似前缀和父路径均不授权。
- 请求的 `pluginKey` 与当前页面插件一致，否则返回 `plugin_mismatch`。
- 桥接已启用；无桥接时前端 SDK 返回 `bridge_unavailable`。

通用 Web 消息上限为 256 KiB。控制台转发只在 WebView 开发模式中启用，单条上限 16 KiB，每个页面每 10 秒最多接收 100 条；生产模式关闭控制台转发。未知消息、过大消息和拒绝原因只做受限诊断日志，避免日志洪泛。

## 开发发现文件

Vite 在插件构建输出目录发布 `.avalonia-web-dev.json`：

```json
{
  "pluginId": "TEMPLATE-PLUGIN-0000-0000-000000000001",
  "origin": "http://127.0.0.1:5173",
  "processId": 12345,
  "startedAt": "2026-08-03T12:00:00.000Z",
  "leaseId": "00000000-0000-4000-8000-000000000000"
}
```

宿主当前读取 `pluginId/origin/processId/startedAt`，JSON 反序列化会忽略 `leaseId` 等未知字段；Vite 插件和调试编排器使用 `leaseId` 判断文件所有权，避免旧进程清理新进程的发现文件。来源必须是带有效端口的本机回环 `http`/`https`，宿主还会校验 PID 存活、进程启动时间和来源健康状态；无效、陈旧或不可达记录会被忽略并回到生产模式。

## 生命周期

Web 应用在插件注册阶段先注册，随后插件注册自己的 IPC 处理器。注册失败时，宿主会清理该插件的静态目录和 IPC 处理器，并把插件置为 `Error`；该失败路径不承诺立即卸载 `AssemblyLoadContext`。可回收的 `AssemblyLoadContext` 会在插件禁用且运行中操作已空闲、注销/卸载、宿主释放等明确的运行时清理路径中释放。

当前禁用后再启用只把状态改回 `Installed`，插件会在重启后加载；不要依赖运行中热启或重新构建根依赖注入容器。

## 构建与发布约束

Template 的普通 `dotnet build` 会运行 `pnpm build`，并把 `Web/dist` 复制到输出的 `web/`。`SkipPluginWebBuild=true` 仅用于开发编排器的普通构建：它复制 `dev-placeholder.html` 为 `web/index.html`，让清单和宿主注册完整，但页面实际由 Vite 提供。

发布时必须有真实 `Web/dist/index.html`。即使设置 `SkipPluginWebBuild=true`，如果分发目录不存在也会直接报错；不能把开发占位页当成发布资源。
