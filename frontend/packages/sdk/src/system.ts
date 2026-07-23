/**
 * 系统级 API：文件选择器与对话框
 *
 * 这些命令由宿主（LYBox.Plugin.Shared.SystemCommands）自动注册到每个 WebView 实例，
 * 前端无需额外配置即可调用。
 *
 * @example
 * ```ts
 * import { openFilePicker, showMessageBox } from '@lybox/sdk';
 *
 * // 打开文件选择器
 * const files = await openFilePicker({
 *   title: '选择图片',
 *   multiple: true,
 *   filters: [{ name: '图片', extensions: ['png', 'jpg', 'jpeg'] }]
 * });
 *
 * // 显示消息框
 * await showMessageBox({ message: '操作完成', icon: 'success' });
 * ```
 */

import { rpc } from './rpc';

// ==================== 类型定义 ====================

/** 文件过滤器（与 Avalonia FilePickerFileType 对应） */
export interface FileFilter {
  /** 过滤器显示名称，如"图片" */
  name: string;
  /** 扩展名列表（不含点），如 ['png', 'jpg'] */
  extensions: string[];
}

/** 打开文件选择器选项 */
export interface OpenFilePickerOptions {
  /** 对话框标题 */
  title?: string;
  /** 是否允许多选（默认 false） */
  multiple?: boolean;
  /** 文件类型过滤器 */
  filters?: FileFilter[];
}

/** 保存文件选择器选项 */
export interface SaveFilePickerOptions {
  /** 对话框标题 */
  title?: string;
  /** 建议的文件名（不含路径） */
  suggestedFileName?: string;
  /** 文件类型过滤器 */
  filters?: FileFilter[];
}

/** 打开文件夹选择器选项 */
export interface OpenFolderPickerOptions {
  /** 对话框标题 */
  title?: string;
  /** 是否允许多选（默认 false） */
  multiple?: boolean;
}

/** 消息框图标 */
export type MessageBoxIcon = 'info' | 'warning' | 'error' | 'success';

/** 消息框按钮组合 */
export type MessageBoxButton = 'OK' | 'YesNo' | 'YesNoCancel';

/** 消息框选项 */
export interface MessageBoxOptions {
  /** 消息内容 */
  message: string;
  /** 对话框标题 */
  title?: string;
  /** 按钮组合（默认 'OK'） */
  button?: MessageBoxButton;
  /** 图标（默认 'info'） */
  icon?: MessageBoxIcon;
}

/** 确认对话框选项 */
export interface ConfirmDialogOptions {
  /** 消息内容 */
  message: string;
  /** 对话框标题 */
  title?: string;
  /** 图标（默认 'warning'） */
  icon?: MessageBoxIcon;
}

/** 消息框按钮结果 */
export type MessageBoxResult = 'OK' | 'Yes' | 'No' | 'Cancel';

// ==================== 便捷方法 ====================

/**
 * 打开文件选择器
 *
 * @param options 选项
 * @returns 选中文件的路径数组（未选择时为空数组）
 *
 * @example
 * ```ts
 * const files = await openFilePicker({
 *   title: '选择文件',
 *   multiple: true,
 *   filters: [{ name: '文本文件', extensions: ['txt', 'md'] }]
 * });
 * if (files.length > 0) {
 *   console.log('选中:', files);
 * }
 * ```
 */
export async function openFilePicker(options?: OpenFilePickerOptions): Promise<string[]> {
  const result = await rpc<string[] | { error: string }>('OpenFilePicker', options ?? {});
  if (Array.isArray(result)) return result;
  throw new Error((result as { error: string }).error ?? '文件选择器调用失败');
}

/**
 * 打开保存文件对话框
 *
 * @param options 选项
 * @returns 选中文件的保存路径（未选择时为 null）
 *
 * @example
 * ```ts
 * const path = await saveFilePicker({
 *   suggestedFileName: 'untitled.txt',
 *   filters: [{ name: '文本文件', extensions: ['txt'] }]
 * });
 * if (path) {
 *   console.log('保存到:', path);
 * }
 * ```
 */
export async function saveFilePicker(options?: SaveFilePickerOptions): Promise<string | null> {
  const result = await rpc<string | null | { error: string }>('SaveFilePicker', options ?? {});
  if (result === null || typeof result === 'string') return result;
  throw new Error((result as { error: string }).error ?? '保存文件对话框调用失败');
}

/**
 * 打开文件夹选择器
 *
 * @param options 选项
 * @returns 选中文件夹的路径数组（未选择时为空数组）
 *
 * @example
 * ```ts
 * const folders = await openFolderPicker({
 *   title: '选择项目目录',
 *   multiple: false
 * });
 * if (folders.length > 0) {
 *   console.log('目录:', folders[0]);
 * }
 * ```
 */
export async function openFolderPicker(options?: OpenFolderPickerOptions): Promise<string[]> {
  const result = await rpc<string[] | { error: string }>('OpenFolderPicker', options ?? {});
  if (Array.isArray(result)) return result;
  throw new Error((result as { error: string }).error ?? '文件夹选择器调用失败');
}

/**
 * 显示消息框
 *
 * @param options 选项
 * @returns 按钮结果（'OK' | 'Yes' | 'No' | 'Cancel'）
 *
 * @example
 * ```ts
 * const result = await showMessageBox({
 *   message: '确定要删除吗？',
 *   title: '确认',
 *   button: 'YesNo',
 *   icon: 'warning'
 * });
 * if (result === 'Yes') {
 *   // 执行删除
 * }
 * ```
 */
export async function showMessageBox(options: MessageBoxOptions): Promise<MessageBoxResult> {
  return rpc<MessageBoxResult>('ShowMessageBox', options);
}

/**
 * 显示确认对话框（Yes/No）
 *
 * @param options 选项
 * @returns 用户是否点击了"是"
 *
 * @example
 * ```ts
 * const confirmed = await showConfirmDialog({
 *   message: '确定要保存更改吗？',
 *   icon: 'warning'
 * });
 * if (confirmed) {
 *   await save();
 * }
 * ```
 */
export async function showConfirmDialog(options: ConfirmDialogOptions): Promise<boolean> {
  return rpc<boolean>('ShowConfirmDialog', options);
}
