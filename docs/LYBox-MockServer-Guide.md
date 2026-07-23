# LYBox.MockServer 使用手册

LYBox.MockServer 是一个独立运行的 dotnet tool，为 LYBox WebView 插件前端提供 **Mock 后端**，无需启动 Avalonia 宿主即可在浏览器中调试前端页面、验证 RPC 调用与 SSE 推送逻辑。

> **适用版本**：HostVersion 2.2.0+
> **命令名**：`lybox-mock`
> **NuGet 包**：`LYBox.MockServer`（[nuget.org](https://www.nuget.org/packages/LYBox.MockServer)）
> **源码位置**：`tools/LYBox.MockServer/`

---

## 目录

- [设计目标](#设计目标)
- [安装](#安装)
- [快速开始](#快速开始)
- [命令行参数](#命令行参数)
- [HTTP 端点](#http-端点)
- [mock.json 数据格式](#mockjson-数据格式)
- [前端集成](#前端集成)
- [调试技巧](#调试技巧)
- [与真实后端的差异](#与真实后端的差异)
- [常见问题](#常见问题)

---

## 设计目标

| 目标 | 说明 |
|------|------|
| **零宿主调试** | 无需编译启动 Avalonia 应用，前端开发者可在浏览器中独立调试 |
| **代码零修改** | 前端同一份 JS 代码在 WebView / 浏览器 / Mock 三种环境运行无需改动 |
| **Mock RPC** | 根据 `mock.json` 返回预设数据，支持延迟模拟 |
| **Mock SSE** | 定时推送 `tick` 事件，验证前端事件订阅逻辑 |
| **静态资源服务** | 直接托管插件 `wwwroot/` 目录，支持 HTML/CSS/JS/图片等 |

---

## 安装

### 从 nuget.org 安装（推荐）

```bash
dotnet tool install --global LYBox.MockServer
```

### 从本地构建安装

```bash
# 1. 构建 tool NuGet 包
.\build.ps1 --build=tool

# 2. 从本地 feed 安装
dotnet tool install --global --add-source bin/tools LYBox.MockServer
```

### 验证安装

```bash
lybox-mock --help
```

输出：

```
LYBox Mock Server —— 前端调试 Mock 后端

用法:
  lybox-mock [options]

选项:
  --port <port>           监听端口（默认 5173）
  --wwwroot <path>        插件 wwwroot 目录（默认自动检测）
  --mock <path>           mock.json 路径（默认 <wwwroot>/.lybox/mock.json）
  --plugin <pluginId>     默认 pluginId（仅用于日志提示，默认 WebTemplate）
  --help, -h              显示帮助

端点:
  GET  /__lybox/ipc.js     返回 ipc.js
  POST /__rpc              Mock RPC
  GET  /sse/{pluginId}     Mock SSE
  GET  /{pluginId}/{path}  静态资源

示例:
  lybox-mock
  lybox-mock --port 8080 --wwwroot ./plugins/LYBox.Plugin.WebTemplate/wwwroot
```

### 升级与卸载

```bash
# 升级到最新版
dotnet tool update --global LYBox.MockServer

# 卸载
dotnet tool uninstall --global LYBox.MockServer
```

---

## 快速开始

### 1. 进入仓库根目录

```bash
cd f:\Code\Dotnet\AvaloniaTemplate
```

Mock Server 会自动向上遍历查找 `plugins/` 目录定位 `wwwroot`。

### 2. 启动 Mock Server

```bash
lybox-mock
```

输出：

```
[mock-server] wwwroot: f:\Code\Dotnet\AvaloniaTemplate\plugins\LYBox.Plugin.WebTemplate\wwwroot
[mock-server] mock.json: f:\Code\Dotnet\AvaloniaTemplate\plugins\LYBox.Plugin.WebTemplate\wwwroot\.lybox\mock.json
[mock-server] ipc.js: f:\Code\Dotnet\AvaloniaTemplate\src\LYBox.Plugin.Shared\Rpc\Assets\ipc.js
[mock-server] 启动中: http://localhost:5173
[mock-server] 前端入口: http://localhost:5173/8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d/index.html
[mock-server] 按 Ctrl+C 停止
```

### 3. 在浏览器打开前端入口

```
http://localhost:5173/8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d/index.html
```

> `8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d` 是 WebTemplate 插件的 PluginId（UUID 形式）。

### 4. 调试 RPC 与 SSE

页面加载后：
- 点击 **GreetAsync** 按钮 → 触发 `POST /__rpc`，返回 mock 数据
- 点击 **AddAsync** / **GetPluginInfoAsync** → 同上
- SSE 自动连接 `GET /sse/{pluginId}`，每 2 秒推送 `tick` 事件，页面右上角徽章计数递增

---

## 命令行参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `--port <port>` | HTTP 监听端口 | `5173` |
| `--wwwroot <path>` | 插件 wwwroot 目录路径 | 自动检测（向上遍历查找 `plugins/LYBox.Plugin.WebTemplate/wwwroot`） |
| `--mock <path>` | mock.json 文件路径 | `<wwwroot>/.lybox/mock.json` |
| `--plugin <pluginId>` | 默认 pluginId（仅用于日志提示与 URL 生成） | `8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d`（WebTemplate） |
| `--help` / `-h` | 显示帮助 | — |

### 常见组合

```bash
# 默认配置（WebTemplate 插件，端口 5173）
lybox-mock

# 自定义端口
lybox-mock --port 8080

# 调试其他插件（需指定 wwwroot）
lybox-mock --wwwroot ./plugins/LYBox.Plugin.Other/wwwroot --plugin <other-plugin-id>

# 使用独立的 mock 数据文件
lybox-mock --mock ./my-mock-data.json

# 完整指定
lybox-mock --port 3000 --wwwroot ./plugins/LYBox.Plugin.WebTemplate/wwwroot --mock ./custom-mock.json
```

---

## HTTP 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/` | 健康检查，返回服务信息 JSON |
| `GET` | `/__lybox/ipc.js` | 返回 `ipc.js` 脚本（供 HTML `<script>` 引入） |
| `POST` | `/__rpc` | Mock RPC，根据 `mock.json` 返回预设数据 |
| `GET` | `/sse/{pluginId}` | Mock SSE，建立 Server-Sent Events 连接，定时推送事件 |
| `POST` | `/__emit` | 浏览器模式事件 emit（接收并返回 202，不处理） |
| `POST` | `/__channel/close` | 通道关闭（返回 202） |
| `GET` | `/{pluginId}/{**path}` | 静态资源服务（从 wwwroot 提供文件） |

### 端点详解

#### `GET /` 健康检查

```bash
curl http://localhost:5173/
```

```json
{
  "service": "LYBox.MockServer",
  "port": 5173,
  "wwwroot": "f:\\Code\\Dotnet\\AvaloniaTemplate\\plugins\\LYBox.Plugin.WebTemplate\\wwwroot"
}
```

#### `POST /__rpc` Mock RPC

请求体：

```json
{
  "name": "GreetAsync",
  "args": ["World"],
  "callbackId": "cb_1"
}
```

响应（成功）：

```json
{
  "result": "Hello, Mock! 这是来自 mock 后端的问候。"
}
```

响应（命令未定义，HTTP 404）：

```json
{
  "error": "命令未在 mock.json 中定义: GreetAsync"
}
```

响应（请求体无效，HTTP 400）：

```json
{
  "error": "请求体无效"
}
```

#### `GET /sse/{pluginId}` Mock SSE

建立 SSE 连接后，服务器先发送 `ready` 事件：

```
event: ready
data: {"pluginId":"8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d"}
```

随后按 `mock.json` 中 `_sseEvents.tick.intervalMs` 配置的间隔（默认 2000ms）定时推送 `dispatch` 事件：

```
event: dispatch
data: {"name":"tick","data":{"count":1,"time":"14:30:25","message":"Mock 推送 #1（来自 lybox-mock）"}}
```

#### `GET /{pluginId}/{**path}` 静态资源

从 wwwroot 提供静态文件，支持路径穿越防护。

示例：

```
GET /8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d/index.html    → wwwroot/index.html
GET /8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d/css/app.css   → wwwroot/css/app.css
```

支持的 MIME 类型自动识别：`.html`、`.css`、`.js`、`.mjs`、`.json`、`.svg`、`.png`、`.jpg`、`.gif`、`.ico`、`.woff`、`.woff2`、`.ttf`、`.map`、`.webp`、`.wasm`。

---

## mock.json 数据格式

`mock.json` 是 Mock 数据的核心配置文件，默认位于 `<wwwroot>/.lybox/mock.json`。

### 完整示例

```json
{
  "GreetAsync": {
    "delay": 100,
    "result": "Hello, Mock! 这是来自 mock 后端的问候。"
  },
  "AddAsync": {
    "delay": 50,
    "result": 42
  },
  "GetPluginInfoAsync": {
    "delay": 80,
    "result": {
      "id": "8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d",
      "name": "Web Template (Mock)",
      "version": "1.0.0-mock",
      "serverTime": "2026-07-23T00:00:00.000Z"
    }
  },
  "OpenFilePicker": {
    "delay": 100,
    "result": ["C:/mock/example.txt", "C:/mock/sample.json"]
  },
  "SaveFilePicker": {
    "delay": 100,
    "result": "C:/mock/untitled.txt"
  },
  "OpenFolderPicker": {
    "delay": 100,
    "result": ["C:/mock/projects"]
  },
  "ShowMessageBox": {
    "delay": 100,
    "result": "OK"
  },
  "ShowConfirmDialog": {
    "delay": 100,
    "result": true
  },
  "_sseEvents": {
    "tick": {
      "intervalMs": 2000,
      "data": {
        "count": 1,
        "time": "00:00:00",
        "message": "Mock 推送（来自 lybox-mock）"
      }
    }
  }
}
```

### 字段说明

#### RPC 命令条目

键名 = RPC 命令名（与前端 `window.__lybox.rpc('CommandName', ...)` 调用的第一个参数一致）。

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `delay` | number (ms) | 否 | 模拟网络延迟，默认 `0`（立即返回） |
| `result` | any | 否 | 返回给前端的数据，可以是字符串、数字、对象、数组、`null`。省略时返回 `null` |

#### `_sseEvents` SSE 事件配置

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `_sseEvents.tick.intervalMs` | number (ms) | 否 | 推送间隔，默认 `2000` |
| `_sseEvents.tick.data` | object | 否 | 推送数据模板。支持三个动态字段会被运行时覆盖：<br>- `count`：递增计数（从 1 开始）<br>- `time`：当前时间 `HH:mm:ss`<br>- `message`：自动生成的消息文本 |

### 动态字段覆盖规则

SSE 推送的 `data` 对象中，以下三个字段名会被运行时自动覆盖为动态值，其余字段按 `mock.json` 原样返回：

| 字段名 | 运行时值 |
|--------|---------|
| `count` | 递增整数（1, 2, 3, ...） |
| `time` | `HH:mm:ss` 格式的当前时间 |
| `message` | `"Mock 推送 #{count}（来自 lybox-mock）"` |

### 添加自定义 RPC 命令

在 `mock.json` 中添加新条目即可：

```json
{
  "GreetAsync": { "delay": 100, "result": "Hello, Mock!" },
  "GetUserInfoAsync": {
    "delay": 200,
    "result": {
      "userId": 1001,
      "username": "mock-user",
      "roles": ["admin", "viewer"]
    }
  },
  "DeleteItemAsync": {
    "delay": 300,
    "result": null
  }
}
```

前端调用：

```javascript
const userInfo = await window.__lybox.rpc('GetUserInfoAsync');
```

### 内置系统级命令

宿主通过 `SystemCommands` 自动注册以下 5 个系统命令到每个 WebView，Mock Server 已内置默认响应。在浏览器调试模式下，这些命令会返回 mock 数据而非真实调用系统对话框：

| 命令名 | Mock 返回值 | 真实行为 |
|--------|------------|---------|
| `OpenFilePicker` | `["C:/mock/example.txt"]` | 打开系统文件选择器，返回选中文件路径数组 |
| `SaveFilePicker` | `"C:/mock/untitled.txt"` | 打开保存文件对话框，返回保存路径或 null |
| `OpenFolderPicker` | `["C:/mock/projects"]` | 打开文件夹选择器，返回路径数组 |
| `ShowMessageBox` | `"OK"` | 显示 Ursa 消息框，返回按钮结果 |
| `ShowConfirmDialog` | `true` | 显示确认对话框，返回布尔值 |

可在 `mock.json` 中自定义这些命令的 mock 响应：

```json
{
  "OpenFilePicker": {
    "delay": 200,
    "result": ["/Users/mock/documents/report.pdf"]
  },
  "ShowConfirmDialog": {
    "delay": 0,
    "result": false
  }
}
```

> 详细用法见 [WebView IPC 使用指南 — 系统级命令](./WebView-IPC-Guide.md#系统级命令systemcommands)。

---

## 前端集成

### HTML 引入 ipc.js

前端页面必须通过 `<script>` 标签引入 `ipc.js`，它提供 `window.__lybox` 全局对象：

```html
<script src="/__lybox/ipc.js"></script>
```

> 在 WebView 环境中，`ipc.js` 由宿主自动注入，无需手动引入。浏览器模式下必须手动引入。

### 调用 RPC

```javascript
// 调用 mock.json 中定义的命令
const result = await window.__lybox.rpc('GreetAsync', 'World');
const sum = await window.__lybox.rpc('AddAsync', 3, 5);
const info = await window.__lybox.rpc('GetPluginInfoAsync');
```

`window.__lybox.rpc` 返回 Promise，参数为 `(commandName, ...args)`。

### 订阅 SSE 事件

```javascript
// 等待 __lybox 就绪
(function waitForLybox() {
    if (window.__lybox) {
        window.__lybox.on('tick', function (data) {
            console.log('收到 tick 事件:', data);
            // data.count     递增计数
            // data.time      时间戳
            // data.message   消息文本
        });
    } else {
        setTimeout(waitForLybox, 100);
    }
})();
```

### 环境检测

`ipc.js` 自动检测运行环境，前端可通过 `window.__lybox.isWebView` 判断：

```javascript
if (window.__lybox.isWebView) {
    // WebView 模式：使用原生 IPC（invokeCSharpAction）
} else {
    // 浏览器模式：使用 HTTP fetch（自动走 /__rpc）
}
```

---

## 调试技巧

### 1. 查看实时日志

Mock Server 在控制台输出所有 RPC 调用：

```
[mock-server] RPC 调用: GreetAsync
[mock-server] RPC 调用: AddAsync
```

### 2. 使用 curl 测试端点

```bash
# 健康检查
curl http://localhost:5173/

# 测试 RPC
curl -X POST http://localhost:5173/__rpc \
  -H "Content-Type: application/json" \
  -d '{"name":"GreetAsync","args":["World"],"callbackId":"cb_1"}'

# 查看 ipc.js
curl http://localhost:5173/__lybox/ipc.js

# 获取静态资源
curl http://localhost:5173/8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d/index.html
```

### 3. 浏览器 DevTools

打开浏览器开发者工具：
- **Network** 标签：查看 `/__rpc` 请求与响应
- **EventSource** 连接：查看 `/sse/{pluginId}` 的 SSE 流
- **Console**：查看 `ipc.js` 日志输出

### 4. 修改 mock.json 后重启

`mock.json` 在启动时一次性加载到内存。修改后需 **Ctrl+C 停止并重新启动** Mock Server 才能生效。

### 5. 同时调试多个插件

为不同插件启动不同端口的 Mock Server：

```bash
# 终端 1：调试 WebTemplate
lybox-mock --port 5173

# 终端 2：调试其他插件
lybox-mock --port 5174 --wwwroot ./plugins/LYBox.Plugin.Other/wwwroot --plugin <other-plugin-id>
```

### 6. 模拟慢网络

通过 `delay` 字段模拟慢网络，验证前端 loading 状态：

```json
{
  "SlowApiAsync": {
    "delay": 3000,
    "result": "延迟 3 秒返回"
  }
}
```

---

## 与真实后端的差异

| 方面 | Mock Server | 真实宿主（WebHostService） |
|------|-------------|--------------------------|
| 运行环境 | 独立 ASP.NET Core 进程 | Avalonia 应用内嵌 Kestrel |
| RPC 处理 | 查 `mock.json` 返回静态数据 | 执行真实 C# 命令处理器 |
| SSE 推送 | 定时推送 `tick` 事件 | 由业务逻辑主动推送 |
| 静态资源 | 从 wwwroot 目录提供 | 同（WebHostService 也从 wwwroot 提供） |
| ipc.js 注入 | 需 HTML 手动 `<script>` 引入 | 宿主自动注入到 WebView |
| 调试面板 | 无 | `GET /__lybox/debug`（仅 DEBUG 配置） |
| 传输层 | HTTP fetch | WebView 原生 IPC + HTTP fetch 双通道 |

**关键保证**：前端 JS 代码在两种环境下**完全一致**，无需任何条件分支。`ipc.js` 内部自动检测 `invokeCSharpAction` 是否存在来选择传输层。

---

## 常见问题

### Q1：启动报错"找不到 wwwroot 目录"

**原因**：Mock Server 向上遍历 6 层目录未找到 `plugins/` 目录。

**解决**：
- 确保在仓库根目录或子目录中运行
- 或通过 `--wwwroot` 显式指定路径：
  ```bash
  lybox-mock --wwwroot f:\Code\Dotnet\AvaloniaTemplate\plugins\LYBox.Plugin.WebTemplate\wwwroot
  ```

### Q2：页面打开后 RPC 调用返回 404

**原因**：`mock.json` 中未定义该命令名。

**解决**：
- 检查前端调用的命令名与 `mock.json` 的键名是否完全一致（区分大小写）
- 在 `mock.json` 中添加对应条目

### Q3：SSE 连接建立但无事件推送

**原因**：`mock.json` 中缺少 `_sseEvents.tick` 配置。

**解决**：在 `mock.json` 中添加：
```json
{
  "_sseEvents": {
    "tick": {
      "intervalMs": 2000,
      "data": { "count": 1, "time": "00:00:00", "message": "测试" }
    }
  }
}
```

### Q4：端口被占用

**解决**：通过 `--port` 指定其他端口：
```bash
lybox-mock --port 8080
```

### Q5：修改了 mock.json 但不生效

**原因**：`mock.json` 在启动时加载到内存，运行时不重新读取。

**解决**：Ctrl+C 停止 Mock Server，重新启动。

### Q6：前端页面加载但 `window.__lybox` 未定义

**原因**：HTML 未引入 `ipc.js`。

**解决**：在 HTML `<head>` 或 `<body>` 中添加：
```html
<script src="/__lybox/ipc.js"></script>
```

### Q7：如何调试非 WebTemplate 插件

```bash
# 1. 确保目标插件有 wwwroot 目录
# 2. 指定 wwwroot 和 pluginId 启动
lybox-mock --wwwroot ./plugins/LYBox.Plugin.YourPlugin/wwwroot --plugin <your-plugin-id>
```

`<your-plugin-id>` 可在插件的 `.csproj` 文件中 `<PluginId>` 标签查看。

### Q8：ipc.js 路径显示"(未找到)"

**原因**：Mock Server 向上遍历未找到 `src/LYBox.Plugin.Shared/Rpc/Assets/ipc.js`。

**影响**：`GET /__lybox/ipc.js` 端点将返回 `// ipc.js not found`，前端 `window.__lybox` 不会初始化。

**解决**：确保在仓库内运行，或从源码构建以包含最新的 `ipc.js`。

---

## 相关文档

- [WebView IPC 使用指南](./WebView-IPC-Guide.md) — WebView 双向通讯框架完整说明
- [插件开发指南](./Plugin-Components-Guide.md) — 插件系统架构与开发流程
- [插件 API 参考](./Plugin-API-Reference.md) — 插件 SDK API 文档
