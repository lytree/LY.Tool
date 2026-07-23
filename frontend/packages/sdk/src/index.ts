/**
 * @lybox/sdk —— LYBox WebView 前端 SDK
 *
 * 提供与 LYBox 宿主（Avalonia WebView）的双向通讯封装：
 * - RPC 调用（类型安全）
 * - 事件订阅/发布
 * - 流式 Channel
 * - 环境检测
 * - 调试工具
 * - Fluent Design 主题（与宿主配色一致）
 *
 * @example
 * ```ts
 * import { rpc, on, isWebView, setTheme } from '@lybox/sdk';
 * import '@lybox/sdk/css';
 *
 * // 调用宿主命令
 * const greeting = await rpc<string>('GreetAsync', 'World');
 *
 * // 订阅 SSE 事件
 * on<{ count: number }>('tick', (data) => {
 *   console.log(`Tick #${data.count}`);
 * });
 *
 * // 环境检测
 * if (isWebView()) {
 *   console.log('运行在 WebView 内');
 * }
 *
 * // 主题切换
 * setTheme('dark');
 * ```
 */

// 核心 API
export { rpc, rpcChannel } from './rpc';
export { on, off, emit, whenReady } from './events';
export { createChannel } from './channel';

// 环境检测
export {
  waitForLybox,
  getEnvironment,
  isWebView,
  isBrowser,
  type LyboxRuntime,
} from './env';

// 调试工具
export {
  getBindings,
  getDebugInfo,
  mountDebugPanel,
} from './debug';

// 系统级 API（文件选择器 + 对话框）
export {
  openFilePicker,
  saveFilePicker,
  openFolderPicker,
  showMessageBox,
  showConfirmDialog,
} from './system';
export type {
  FileFilter,
  OpenFilePickerOptions,
  SaveFilePickerOptions,
  OpenFolderPickerOptions,
  MessageBoxIcon,
  MessageBoxButton,
  MessageBoxOptions,
  ConfirmDialogOptions,
  MessageBoxResult,
} from './system';

// 主题（合并自 @lybox/theme）
export {
  tokens,
  setTheme,
  getTheme,
  toggleTheme,
  restoreTheme,
} from './theme';
export type { ThemeMode, DesignTokens } from './theme';

// 类型
export type {
  BindingManifestEntry,
  ChannelDescriptor,
  Channel,
  EventHandler,
  Unsubscribe,
  EnvironmentInfo,
  DebugPanelOptions,
} from './types';
export { RpcError } from './types';
