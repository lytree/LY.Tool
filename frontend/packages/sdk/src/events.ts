/**
 * 事件订阅/发布封装
 */
import { waitForLybox } from './env';
import type { EventHandler, Unsubscribe } from './types';

/**
 * 订阅宿主推送的事件
 *
 * @param name 事件名
 * @param handler 回调函数
 * @returns 取消订阅函数
 *
 * @example
 * ```ts
 * const unsubscribe = on<{ count: number; time: string }>('tick', (data) => {
 *   console.log(`收到 tick: ${data.count} @ ${data.time}`);
 * });
 * // 之后取消订阅
 * unsubscribe();
 * ```
 */
export function on<T = unknown>(name: string, handler: EventHandler<T>): Unsubscribe {
  let unsub: Unsubscribe | null = null;
  waitForLybox().then((rt) => {
    const wrapped = (data: unknown) => handler(data as T);
    unsub = rt.on(name, wrapped);
  });
  return () => unsub?.();
}

/**
 * 取消事件订阅
 */
export function off(name: string, handler: EventHandler): void {
  const rt = window.__lybox;
  if (rt) {
    rt.off(name, handler);
  }
}

/**
 * 向宿主发送事件
 *
 * @param name 事件名
 * @param data 事件数据
 *
 * @example
 * ```ts
 * emit('userAction', { type: 'click', target: 'button-save' });
 * ```
 */
export function emit(name: string, data?: unknown): void {
  const rt = window.__lybox;
  if (rt) {
    rt.emit(name, data);
  } else {
    console.warn(`[LYBox SDK] __lybox 未就绪，事件 ${name} 未发送`);
  }
}

/**
 * 等待 __lybox 运行时就绪后执行回调
 *
 * @example
 * ```ts
 * whenReady(() => {
 *   on('tick', handleTick);
 * });
 * ```
 */
export function whenReady(callback: () => void): void {
  waitForLybox().then(() => callback()).catch(() => {
    // 超时后仍尝试执行，适合浏览器开发模式
    callback();
  });
}
