/**
 * 环境检测与全局 __lybox 类型声明
 */
import type { BindingManifestEntry, EnvironmentInfo } from './types';

/**
 * window.__lybox 运行时对象（由 ipc.js 注入）
 */
export interface LyboxRuntime {
  /** 是否在 WebView 内运行 */
  isWebView: boolean;

  /** 统一 RPC 入口 */
  rpc(name: string, ...args: unknown[]): Promise<unknown>;

  /** rpc 的别名 */
  invoke(name: string, args: unknown[]): Promise<unknown>;

  /** C# → JS 回推 RPC 结果（内部使用） */
  resolve(callbackId: string, err: string | null, result: unknown): void;

  /** 订阅事件，返回取消订阅函数 */
  on(name: string, cb: (data: unknown) => void): () => void;

  /** 取消事件订阅 */
  off(name: string, cb: (data: unknown) => void): void;

  /** JS → C# 发送事件 */
  emit(name: string, data: unknown): void;

  /** C# → JS 本地分发事件（内部使用） */
  dispatch(name: string, data: unknown): void;

  /** 接收宿主注入的绑定清单（当前为 noop） */
  setBindings(manifestJson: string): void;

  /** 启动 SSE 监听 */
  startSse(pluginId: string): void;

  /** Channel 辅助 API */
  channel: {
    on(id: string, cb: (data: unknown) => void): () => void;
    onData(id: string, data: unknown): void;
    onClose(id: string): void;
  };
}

declare global {
  interface Window {
    __lybox?: LyboxRuntime;
    __lyboxBindings?: BindingManifestEntry[];
    /** WebView 原生 IPC 入口（由 WebView2/WKWebView 注入） */
    invokeCSharpAction?: (body: string) => void;
  }
}

/**
 * 等待 __lybox 运行时就绪
 * @param timeout 超时毫秒数（默认 5000ms）
 */
export function waitForLybox(timeout = 5000): Promise<LyboxRuntime> {
  return new Promise((resolve, reject) => {
    if (window.__lybox) {
      resolve(window.__lybox);
      return;
    }

    const timer = setTimeout(() => {
      clearInterval(pollTimer);
      reject(new Error(`等待 __lybox 就绪超时（${timeout}ms）。请确保已通过 <script src="/__lybox/ipc.js"></script> 引入 ipc.js。`));
    }, timeout);

    const pollTimer = setInterval(() => {
      if (window.__lybox) {
        clearTimeout(timer);
        clearInterval(pollTimer);
        resolve(window.__lybox);
      }
    }, 50);
  });
}

/**
 * 获取环境信息
 */
export function getEnvironment(): EnvironmentInfo {
  const rt = window.__lybox;
  return {
    isWebView: rt?.isWebView ?? false,
    transport: rt?.isWebView ? 'webview-ipc' : 'http',
    ready: !!rt,
  };
}

/**
 * 是否在 WebView 内运行
 */
export function isWebView(): boolean {
  return window.__lybox?.isWebView ?? false;
}

/**
 * 是否在浏览器模式（Mock 或开发环境）
 */
export function isBrowser(): boolean {
  return !isWebView();
}
