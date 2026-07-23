/**
 * RPC 调用封装
 */
import { waitForLybox } from './env';
import type { Channel, ChannelDescriptor } from './types';
import { RpcError } from './types';
import { createChannel } from './channel';

/**
 * 调用宿主 RPC 命令（类型安全版）
 *
 * @param name 命令短名（与 mock.json 键名或 RegisterCommand 注册名一致）
 * @param args 命令参数
 * @returns Promise<返回值>
 *
 * @example
 * ```ts
 * const greeting = await rpc<string>('GreetAsync', 'World');
 * const sum = await rpc<number>('AddAsync', 3, 5);
 * ```
 */
export async function rpc<T = unknown>(name: string, ...args: unknown[]): Promise<T> {
  const rt = await waitForLybox();
  const result = await rt.rpc(name, ...args);
  return result as T;
}

/**
 * 调用 RPC 命令，返回值可能是 Channel（流式通道）
 *
 * @example
 * ```ts
 * const ch = await rpcChannel<number>('StreamNumbers');
 * const unsubscribe = ch.onData(n => console.log(n));
 * ch.onClose(() => console.log('closed'));
 * ```
 */
export async function rpcChannel<T = unknown>(name: string, ...args: unknown[]): Promise<Channel<T>> {
  const descriptor = await rpc<ChannelDescriptor<T>>(name, ...args);
  if (!descriptor || descriptor.__channel !== true) {
    throw new RpcError(`命令 ${name} 未返回 Channel 描述符`, name);
  }
  return createChannel<T>(descriptor.id);
}
