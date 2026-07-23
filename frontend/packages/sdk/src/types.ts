/**
 * LYBox SDK 共享类型定义
 */

/**
 * RPC 命令清单项（由宿主 InjectBindingsAsync 下发）
 */
export interface BindingManifestEntry {
  /** 命令短名（与 rpc() 第一个参数一致） */
  name: string;
}

/**
 * Channel 描述符（rpc 返回值中 __channel=true 时）
 */
export interface ChannelDescriptor<T = unknown> {
  __channel: true;
  id: string;
  itemType: 'stream';
}

/**
 * RPC 错误
 */
export class RpcError extends Error {
  constructor(
    message: string,
    public readonly command: string,
    public readonly callbackId?: string,
  ) {
    super(message);
    this.name = 'RpcError';
  }
}

/**
 * 事件回调类型
 */
export type EventHandler<T = unknown> = (data: T) => void;

/**
 * 取消订阅函数
 */
export type Unsubscribe = () => void;

/**
 * 环境信息
 */
export interface EnvironmentInfo {
  /** 是否在 WebView 内运行 */
  isWebView: boolean;
  /** 传输层类型 */
  transport: 'webview-ipc' | 'http';
  /** 是否已就绪（收到 __lybox:ready） */
  ready: boolean;
}

/**
 * 调试面板配置
 */
export interface DebugPanelOptions {
  /** 容器挂载点（默认 document.body） */
  container?: HTMLElement;
  /** 是否默认展开 */
  defaultOpen?: boolean;
  /** 自定义标题 */
  title?: string;
}

/**
 * Channel 实例（流式数据通道）
 */
export interface Channel<T = unknown> {
  /** 通道 ID */
  readonly id: string;
  /** 订阅数据 */
  onData(handler: (data: T) => void): Unsubscribe;
  /** 订阅关闭 */
  onClose(handler: () => void): Unsubscribe;
}
