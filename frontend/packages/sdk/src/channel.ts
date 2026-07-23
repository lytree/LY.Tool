/**
 * Channel（流式数据通道）封装
 */
import { waitForLybox } from './env';
import type { Channel as IChannel, Unsubscribe } from './types';

/**
 * 创建 Channel 实例
 */
export function createChannel<T = unknown>(id: string): IChannel<T> {
  return {
    id,
    onData(handler: (data: T) => void): Unsubscribe {
      let unsub: Unsubscribe | null = null;
      waitForLybox().then((rt) => {
        const wrapped = (data: unknown) => handler(data as T);
        unsub = rt.channel.on(id, wrapped);
      });
      return () => unsub?.();
    },
    onClose(handler: () => void): Unsubscribe {
      let unsub: Unsubscribe | null = null;
      waitForLybox().then((rt) => {
        const wrapped = () => handler();
        // channel.onClose 是内部触发，这里通过 on 监听特殊事件
        unsub = rt.on(`__channel:close:${id}`, wrapped);
      });
      return () => unsub?.();
    },
  };
}
