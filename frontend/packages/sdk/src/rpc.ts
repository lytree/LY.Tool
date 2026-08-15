/**
 * RPC 调用封装
 */
import { waitForLybox } from './env';
import type { Channel, ChannelDescriptor } from './types';
import { RpcError } from './types';
import { createChannel } from './channel';

/** 单个 RPC 方法的参数与返回类型描述。 */
export type RpcMethodDefinition = { args: unknown[]; result: unknown };

/** 插件 RPC 方法表：键为命令名，值描述参数元组与返回类型。 */
export type RpcMethodMap = Record<string, RpcMethodDefinition>;

/** 根据方法表生成的类型安全 RPC 客户端。 */
export type RpcClient<TMethods extends { [TKey in keyof TMethods]: RpcMethodDefinition }> = {
  invoke<TKey extends keyof TMethods & string>(
    name: TKey,
    ...args: TMethods[TKey]['args']
  ): Promise<TMethods[TKey]['result']>;
};

/**
 * 创建一个以完整方法表约束命令名、参数与返回值的客户端。
 * 相比单次 `rpc<TResult>()`，它还能在编译期检查命令名和参数列表。
 */
export function createRpcClient<
  TMethods extends { [TKey in keyof TMethods]: RpcMethodDefinition }
>(): RpcClient<TMethods> {
  return {
    invoke: (name, ...args) => rpc(name, ...args) as Promise<TMethods[typeof name]['result']>,
  };
}

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
