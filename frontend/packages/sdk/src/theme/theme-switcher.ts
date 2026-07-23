/**
 * 主题切换工具
 *
 * 通过设置 document.documentElement 的 data-theme 属性切换浅色/深色主题。
 * CSS 变量定义在 lybox-theme.css 中。
 */
import type { ThemeMode } from './types';

const STORAGE_KEY = 'lybox-theme';

/**
 * 设置主题模式
 *
 * @param mode 'light' | 'dark' | 'auto'
 * - 'light'：强制浅色
 * - 'dark'：强制深色
 * - 'auto'：跟随系统（移除 data-theme 属性，由 prefers-color-scheme 决定）
 *
 * @example
 * ```ts
 * setTheme('dark');  // 切换到深色
 * setTheme('auto');  // 跟随系统
 * ```
 */
export function setTheme(mode: ThemeMode): void {
  const root = document.documentElement;
  if (mode === 'auto') {
    root.removeAttribute('data-theme');
    localStorage.removeItem(STORAGE_KEY);
  } else {
    root.setAttribute('data-theme', mode);
    localStorage.setItem(STORAGE_KEY, mode);
  }
}

/**
 * 获取当前主题模式
 *
 * @returns 'light' | 'dark' | 'auto'
 */
export function getTheme(): ThemeMode {
  const root = document.documentElement;
  const attr = root.getAttribute('data-theme');
  if (attr === 'light' || attr === 'dark') {
    return attr;
  }
  return 'auto';
}

/**
 * 在浅色/深色之间切换（忽略 auto）
 *
 * @returns 切换后的模式
 */
export function toggleTheme(): ThemeMode {
  const current = getTheme();
  let resolved: 'light' | 'dark';

  if (current === 'auto') {
    // auto 模式下，根据系统当前值切换到相反值
    resolved = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'light' : 'dark';
  } else {
    resolved = current === 'light' ? 'dark' : 'light';
  }

  setTheme(resolved);
  return resolved;
}

/**
 * 从 localStorage 恢复上次保存的主题（页面加载时调用）
 *
 * @example
 * ```ts
 * import { restoreTheme } from '@lybox/sdk';
 * restoreTheme();  // 在 main.ts 中调用
 * ```
 */
export function restoreTheme(): void {
  const saved = localStorage.getItem(STORAGE_KEY);
  if (saved === 'light' || saved === 'dark') {
    document.documentElement.setAttribute('data-theme', saved);
  }
}
