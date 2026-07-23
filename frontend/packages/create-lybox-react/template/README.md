# {{PROJECT_NAME}}

LYBox React + Vite + TypeScript 插件前端项目，由 `create-lybox-react` 脚手架生成。

## 简介

本项目是一个 LYBox 插件的前端实现，基于 React 18 + Vite 5 + TypeScript 5 构建。
通过 `@lybox/sdk` 与 LYBox 宿主（Avalonia WebView）或 `lybox-mock` 进行双向 IPC 通信，
样式统一使用 `@lybox/sdk/css` 提供的 CSS 变量，自动适配宿主主题（浅色 / 深色）。

## 环境要求

- Node.js >= 18
- pnpm >= 8

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

开发模式下，本项目的 `vite.config.ts` 已将 `/__lybox`、`/__rpc`、`/sse`、`/__emit`、`/__channel`
等路径代理到 `http://localhost:5173`（即 lybox-mock 默认监听地址）。

启动顺序：

```bash
# 终端 1：启动 Mock 后端（监听 5173）
lybox-mock --port 5173 --wwwroot ./dist

# 终端 2：启动前端开发服务器（监听 5174）
pnpm dev
```

打开浏览器访问 `http://localhost:5174`，即可看到带 Mock 数据的示例界面。

## 目录结构

```
{{PROJECT_NAME}}/
├── src/
│   ├── .lybox/
│   │   └── mock.json        # Mock 数据：RPC 返回值与 SSE 事件定义
│   ├── App.css              # 组件样式（全部使用 @lybox/sdk/css CSS 变量）
│   ├── App.tsx              # 示例组件：RPC 调用 + SSE 订阅 + 主题切换 + 调试面板
│   ├── main.tsx             # 应用入口
│   └── vite-env.d.ts        # Vite 类型声明
├── index.html               # HTML 入口（开发模式引入 /__lybox/ipc.js）
├── package.json
├── tsconfig.json
├── vite.config.ts           # Vite 配置（含 Mock 代理）
└── README.md
```

## 关键依赖

| 包 | 作用 |
|----|------|
| `@lybox/sdk` | 提供 `rpc`、`on`、`isWebView`、`mountDebugPanel` 等 IPC 能力，以及 `setTheme` / `getTheme` / `restoreTheme` 主题管理与 `@lybox/sdk/css` 样式变量 |
| `react` / `react-dom` | React 18 运行时 |
| `vite` / `@vitejs/plugin-react` | 开发与构建工具链 |

## RPC 调用约定

```ts
import { rpc } from '@lybox/sdk';

// rpc<T>(命令名, ...参数)
const prefix = await rpc<string>('GreetAsync', 'World');
const sum = await rpc<number>('AddAsync', 3, 5);
const info = await rpc<PluginInfo>('GetPluginInfoAsync');
```

## 部署到 LYBox 宿主

1. 执行 `pnpm build`，产物位于 `dist/`。
2. 将 `dist/` 目录配置为 LYBox 插件的 WebView wwwroot。
3. 在插件 `.csproj` 的 `PluginId` 与 `mock.json` 中的 `id` 保持一致。
4. 启动宿主后，WebView 自动注入 `ipc.js`，无需手动引用。

## 主题适配

所有颜色、间距、圆角、字体均通过 CSS 变量引用，宿主切换主题时自动响应。
变量定义见 `@lybox/sdk/src/theme/lybox-theme.css`，与宿主 Avalonia FluentDesign 主题一致。

- 颜色：`var(--lybox-color-primary)`、`var(--lybox-color-background-0|1)`、`var(--lybox-color-text-0|1|2|3)`
- 卡片：`var(--lybox-card-background)`、`var(--lybox-card-stroke)`、`var(--lybox-shadow-card)`
- 间距：`var(--lybox-spacing-extra-tight|tight|base|loose)`
- 圆角：`var(--lybox-radius-small|medium)`
- 字号：`var(--lybox-font-size-caption|body|subtitle)`
- 字体：`var(--lybox-font-family)`、`var(--lybox-font-family-mono)`

请勿在样式中硬编码颜色字面量。
