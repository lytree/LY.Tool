# {{PROJECT_NAME}}

LYBox Vue3 + Vite + TypeScript 插件前端模板。

## 简介

基于 Vue 3 + Vite + TypeScript 的 LYBox 插件前端模板，通过 `@lybox/sdk` 与 LYBox 宿主（Avalonia WebView）进行双向 IPC 通讯。

- **RPC 调用**：调用宿主 C# 命令并接收返回值
- **SSE 事件订阅**：接收宿主推送的事件流
- **主题适配**：与宿主 Fluent Design 配色方案一致的浅色/深色主题
- **环境检测**：自动识别 WebView 模式与浏览器开发模式

## 开发命令

```bash
# 安装依赖
pnpm install

# 启动开发服务器（默认端口 5174）
pnpm dev

# 类型检查
pnpm typecheck

# 生产构建（输出到 dist/）
pnpm build

# 预览构建产物
pnpm preview
```

## 配合 lybox-mock 使用

开发模式下，前端通过 Vite 代理将 `/__lybox`、`/__rpc`、`/sse` 等端点转发到 `lybox-mock`（默认监听 5173 端口）。

```bash
# 在另一个终端启动 lybox-mock
lybox-mock --port 5173 --wwwroot ./dist

# 然后启动前端开发服务器
pnpm dev
```

Mock 数据定义在 `src/.lybox/mock.json` 中，包含 RPC 命令的返回值与 SSE 事件推送规则。

## 目录结构

```
{{PROJECT_NAME}}/
├── index.html              # HTML 入口（开发模式引入 ipc.js）
├── package.json
├── tsconfig.json
├── vite.config.ts          # Vite 配置（含开发代理）
└── src/
    ├── main.ts             # 应用入口
    ├── App.vue             # 示例组件（RPC + SSE + 主题切换）
    ├── env.d.ts            # Vue SFC 类型声明
    └── .lybox/
        └── mock.json       # Mock 数据（lybox-mock 读取）
```

## 部署到 LYBox 宿主

构建后，将 `dist/` 目录配置到 LYBox 插件的 WebView 资源路径即可。WebView 模式下 `ipc.js` 由宿主自动注入，无需在 HTML 中显式引用。
