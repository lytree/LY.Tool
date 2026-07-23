/**
 * 调试工具
 *
 * 提供前端开发期的调试辅助功能：
 * - 获取已注册的 RPC 命令清单
 * - 内联调试面板（可挂载到任意容器）
 * - 环境信息展示
 */
import type { BindingManifestEntry, DebugPanelOptions, EnvironmentInfo } from './types';
import { getEnvironment } from './env';

/**
 * 获取宿主下发的绑定清单（命令名列表）
 *
 * 注意：在浏览器/Mock 模式下，清单由 mock-server 的 mock.json 隐式定义，
 * window.__lyboxBindings 可能为 undefined。
 */
export function getBindings(): BindingManifestEntry[] {
  return window.__lyboxBindings ?? [];
}

/**
 * 获取环境信息
 */
export function getDebugInfo(): EnvironmentInfo {
  return getEnvironment();
}

/**
 * 创建并挂载调试面板
 *
 * 调试面板功能：
 * - 显示当前环境（WebView / 浏览器）
 * - 列出已注册的 RPC 命令
 * - 一键调用命令（可输入参数）
 * - SSE 事件流查看器
 *
 * @param options 配置选项
 * @returns 卸载函数（移除调试面板）
 *
 * @example
 * ```ts
 * import { mountDebugPanel } from '@lybox/sdk/debug';
 *
 * // 挂载到 body
 * const unmount = mountDebugPanel();
 *
 * // 按需卸载
 * unmount();
 * ```
 */
export function mountDebugPanel(options: DebugPanelOptions = {}): () => void {
  const container = options.container ?? document.body;
  const defaultOpen = options.defaultOpen ?? false;
  const title = options.title ?? 'LYBox Debug Panel';

  const panel = document.createElement('div');
  panel.id = '__lybox-debug-panel';
  container.appendChild(panel);

  let eventLog: string[] = [];
  const maxLogEntries = 50;

  function render() {
    const env = getEnvironment();
    const bindings = getBindings();
    const envBadge = env.isWebView
      ? '<span style="background:#0078D4;color:#fff;padding:2px 8px;border-radius:4px;font-size:11px;">WebView IPC</span>'
      : '<span style="background:#107C10;color:#fff;padding:2px 8px;border-radius:4px;font-size:11px;">HTTP (Browser)</span>';

    const commandList = bindings.length > 0
      ? bindings.map((b) => `
        <div style="display:flex;gap:8px;align-items:center;padding:6px 0;border-bottom:1px solid #eee;">
          <code style="flex:1;font-size:13px;">${b.name}</code>
          <input type="text" placeholder='[]' style="width:200px;padding:4px 8px;border:1px solid #ccc;border-radius:4px;font-size:12px;" data-cmd="${b.name}" />
          <button onclick="__lyboxDebugInvoke('${b.name}')" style="padding:4px 12px;background:#0078D4;color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:12px;">调用</button>
        </div>
      `).join('')
      : '<p style="color:#999;font-size:13px;">暂无注册命令（浏览器模式下从 mock.json 读取）</p>';

    const eventLogHtml = eventLog.length > 0
      ? eventLog.map((e) => `<div style="padding:2px 0;border-bottom:1px solid #f0f0f0;font-size:12px;font-family:monospace;">${e}</div>`).join('')
      : '<div style="color:#999;font-size:12px;">（等待事件...）</div>';

    panel.innerHTML = `
      <div style="position:fixed;bottom:16px;right:16px;width:420px;max-height:600px;background:#fff;border:1px solid #e0e0e0;border-radius:8px;box-shadow:0 4px 16px rgba(0,0,0,0.15);font-family:-apple-system,'Segoe UI',sans-serif;z-index:99999;overflow:hidden;">
        <div onclick="__lyboxDebugToggle()" style="display:flex;align-items:center;justify-content:space-between;padding:12px 16px;background:#0078D4;color:#fff;cursor:pointer;">
          <span style="font-size:14px;font-weight:600;">${title}</span>
          <span>${envBadge} <span style="margin-left:8px;font-size:12px;">${defaultOpen ? '▼' : '▶'}</span></span>
        </div>
        <div id="__lybox-debug-body" style="display:${defaultOpen ? 'block' : 'none'};max-height:500px;overflow-y:auto;padding:16px;">
          <h3 style="font-size:13px;margin:0 0 8px;color:#1a1a1a;">RPC 命令 (${bindings.length})</h3>
          <div style="margin-bottom:16px;">${commandList}</div>
          <h3 style="font-size:13px;margin:0 0 8px;color:#1a1a1a;">SSE 事件流</h3>
          <div style="max-height:200px;overflow-y:auto;background:#f9f9f9;padding:8px;border-radius:4px;">${eventLogHtml}</div>
          <div id="__lybox-debug-result" style="margin-top:12px;padding:8px;background:#f0f6ff;border-radius:4px;font-family:monospace;font-size:12px;min-height:20px;color:#666;">（调用结果将显示在此）</div>
        </div>
      </div>
    `;
  }

  // 全局调试函数
  (window as any).__lyboxDebugToggle = () => {
    const body = document.getElementById('__lybox-debug-body');
    if (body) {
      body.style.display = body.style.display === 'none' ? 'block' : 'none';
    }
  };

  (window as any).__lyboxDebugInvoke = async (cmdName: string) => {
    const input = panel.querySelector(`input[data-cmd="${cmdName}"]`) as HTMLInputElement;
    const resultEl = document.getElementById('__lybox-debug-result');
    if (!resultEl) return;

    let args: unknown[] = [];
    if (input?.value.trim()) {
      try {
        args = JSON.parse(`[${input.value}]`);
      } catch {
        resultEl.textContent = `参数解析错误: ${input.value}`;
        resultEl.style.color = '#C42B1C';
        return;
      }
    }

    resultEl.textContent = `调用中: ${cmdName}(${JSON.stringify(args).slice(1, -1)})...`;
    resultEl.style.color = '#666';

    try {
      const rt = window.__lybox;
      if (!rt) throw new Error('__lybox 未就绪');
      const result = await rt.rpc(cmdName, ...args);
      resultEl.textContent = JSON.stringify(result, null, 2);
      resultEl.style.color = '#107C10';
    } catch (err) {
      resultEl.textContent = `错误: ${(err as Error).message}`;
      resultEl.style.color = '#C42B1C';
    }
  };

  // 订阅所有事件用于日志
  const eventNames = ['tick', 'dispatch', '__lybox:ready'];
  const unsubs: (() => void)[] = [];

  const rt = window.__lybox;
  if (rt) {
    eventNames.forEach((name) => {
      try {
        const unsub = rt.on(name, (data) => {
          const time = new Date().toLocaleTimeString();
          eventLog.unshift(`[${time}] ${name}: ${JSON.stringify(data).slice(0, 200)}`);
          if (eventLog.length > maxLogEntries) eventLog.pop();
          render();
        });
        unsubs.push(unsub);
      } catch {
        // 忽略不支持的事件名
      }
    });
  }

  render();

  // 返回卸载函数
  return () => {
    unsubs.forEach((u) => u());
    panel.remove();
    delete (window as any).__lyboxDebugToggle;
    delete (window as any).__lyboxDebugInvoke;
  };
}
