<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from 'vue';
import { rpc, on, isWebView, mountDebugPanel, setTheme, getTheme } from '@lybox/sdk';

// ==================== 环境信息 ====================
// 当前是否运行在 LYBox WebView 内（false 表示浏览器开发模式）
const webViewMode = ref<boolean>(false);

// ==================== 主题切换 ====================
// 当前主题模式（'light' | 'dark' | 'auto'）
const currentTheme = ref<'light' | 'dark' | 'auto'>('auto');

// 切换浅色/深色主题
function toggleTheme(): void {
  const next = currentTheme.value === 'dark' ? 'light' : 'dark';
  setTheme(next);
  currentTheme.value = getTheme();
}

// ==================== RPC 调用：GreetAsync ====================
const greetName = ref<string>('World');
const greetResult = ref<string>('');
const greetLoading = ref<boolean>(false);

async function callGreet(): Promise<void> {
  greetLoading.value = true;
  try {
    // 调用宿主命令 GreetAsync，参数为名字字符串
    const prefix = await rpc<string>('GreetAsync', greetName.value);
    greetResult.value = `${prefix}${greetName.value}`;
  } catch (err) {
    greetResult.value = `调用失败：${(err as Error).message}`;
  } finally {
    greetLoading.value = false;
  }
}

// ==================== RPC 调用：AddAsync ====================
const addA = ref<number>(3);
const addB = ref<number>(5);
const addResult = ref<number | null>(null);
const addLoading = ref<boolean>(false);

async function callAdd(): Promise<void> {
  addLoading.value = true;
  try {
    // 调用宿主命令 AddAsync，传入两个数字，返回和
    addResult.value = await rpc<number>('AddAsync', addA.value, addB.value);
  } catch (err) {
    addResult.value = null;
    console.error('AddAsync 失败：', err);
  } finally {
    addLoading.value = false;
  }
}

// ==================== RPC 调用：GetPluginInfoAsync ====================
interface PluginInfo {
  id: string;
  name: string;
  version: string;
}
const pluginInfo = ref<PluginInfo | null>(null);
const infoLoading = ref<boolean>(false);

async function callGetInfo(): Promise<void> {
  infoLoading.value = true;
  try {
    // 获取当前插件信息（id / name / version）
    pluginInfo.value = await rpc<PluginInfo>('GetPluginInfoAsync');
  } catch (err) {
    console.error('GetPluginInfoAsync 失败：', err);
  } finally {
    infoLoading.value = false;
  }
}

// ==================== SSE 事件订阅：tick ====================
interface TickData {
  count: number;
  time: string;
  message?: string;
}
const tickCount = ref<number>(0);
const tickLogs = ref<string[]>([]);
const tickSubscribed = ref<boolean>(false);

// 订阅事件返回的取消订阅函数
let unsubscribeTick: (() => void) | null = null;

function subscribeTick(): void {
  if (tickSubscribed.value) return;
  // 订阅宿主推送的 tick 事件
  unsubscribeTick = on<TickData>('tick', (data) => {
    tickCount.value += 1;
    const time = new Date().toLocaleTimeString();
    tickLogs.value.unshift(`[${time}] count=${data.count} time=${data.time}${data.message ? ' msg=' + data.message : ''}`);
    if (tickLogs.value.length > 50) tickLogs.value.pop();
  });
  tickSubscribed.value = true;
}

function unsubscribeTickEvent(): void {
  if (unsubscribeTick) {
    unsubscribeTick();
    unsubscribeTick = null;
  }
  tickSubscribed.value = false;
}

// ==================== 调试面板 ====================
// 调试面板卸载函数
let unmountDebug: (() => void) | null = null;
const debugMounted = ref<boolean>(false);

function toggleDebugPanel(): void {
  if (debugMounted.value) {
    // 卸载调试面板
    if (unmountDebug) {
      unmountDebug();
      unmountDebug = null;
    }
    debugMounted.value = false;
  } else {
    // 挂载调试面板到 body
    unmountDebug = mountDebugPanel({ defaultOpen: true });
    debugMounted.value = true;
  }
}

// ==================== 生命周期 ====================
onMounted(() => {
  // 初始化环境检测与主题状态
  webViewMode.value = isWebView();
  currentTheme.value = getTheme();

  // 默认订阅 tick 事件，演示 SSE 能力
  subscribeTick();
});

onBeforeUnmount(() => {
  // 组件卸载时清理订阅与调试面板
  unsubscribeTickEvent();
  if (unmountDebug) unmountDebug();
});
</script>

<template>
  <div class="lybox-app">
    <!-- 顶部标题栏 -->
    <header class="lybox-app__header">
      <div class="lybox-app__title">
        <h1>LYBox Vue3 插件示例</h1>
        <span class="lybox-app__badge" :class="webViewMode ? 'lybox-app__badge--webview' : 'lybox-app__badge--browser'">
          {{ webViewMode ? 'WebView IPC' : 'Browser (Mock)' }}
        </span>
      </div>
      <button class="lybox-btn lybox-btn--ghost" @click="toggleTheme">
        {{ currentTheme === 'dark' ? '🌙 深色' : '☀️ 浅色' }}
      </button>
    </header>

    <main class="lybox-app__main">
      <!-- RPC 调用区 -->
      <section class="lybox-card">
        <h2 class="lybox-card__title">RPC 调用</h2>

        <!-- GreetAsync -->
        <div class="lybox-row">
          <label class="lybox-row__label">GreetAsync</label>
          <input
            v-model="greetName"
            class="lybox-input"
            type="text"
            placeholder="输入名字"
            @keyup.enter="callGreet"
          />
          <button class="lybox-btn lybox-btn--primary" :disabled="greetLoading" @click="callGreet">
            {{ greetLoading ? '调用中...' : '调用' }}
          </button>
        </div>
        <p class="lybox-row__result" v-if="greetResult">结果：{{ greetResult }}</p>

        <!-- AddAsync -->
        <div class="lybox-row">
          <label class="lybox-row__label">AddAsync</label>
          <input v-model.number="addA" class="lybox-input lybox-input--num" type="number" />
          <span class="lybox-row__operator">+</span>
          <input v-model.number="addB" class="lybox-input lybox-input--num" type="number" />
          <button class="lybox-btn lybox-btn--primary" :disabled="addLoading" @click="callAdd">
            {{ addLoading ? '调用中...' : '调用' }}
          </button>
        </div>
        <p class="lybox-row__result" v-if="addResult !== null">结果：{{ addResult }}</p>

        <!-- GetPluginInfoAsync -->
        <div class="lybox-row">
          <label class="lybox-row__label">GetPluginInfoAsync</label>
          <button class="lybox-btn lybox-btn--primary" :disabled="infoLoading" @click="callGetInfo">
            {{ infoLoading ? '调用中...' : '获取插件信息' }}
          </button>
        </div>
        <div class="lybox-info" v-if="pluginInfo">
          <div class="lybox-info__item"><span class="lybox-info__key">ID：</span>{{ pluginInfo.id }}</div>
          <div class="lybox-info__item"><span class="lybox-info__key">名称：</span>{{ pluginInfo.name }}</div>
          <div class="lybox-info__item"><span class="lybox-info__key">版本：</span>{{ pluginInfo.version }}</div>
        </div>
      </section>

      <!-- SSE 事件区 -->
      <section class="lybox-card">
        <h2 class="lybox-card__title">SSE 事件订阅（tick）</h2>
        <div class="lybox-row">
          <span class="lybox-row__label">已收到事件数：{{ tickCount }}</span>
          <button
            v-if="!tickSubscribed"
            class="lybox-btn lybox-btn--primary"
            @click="subscribeTick"
          >订阅</button>
          <button
            v-else
            class="lybox-btn lybox-btn--ghost"
            @click="unsubscribeTickEvent"
          >取消订阅</button>
        </div>
        <div class="lybox-log">
          <div v-if="tickLogs.length === 0" class="lybox-log__empty">（等待事件...）</div>
          <div v-for="(log, idx) in tickLogs" :key="idx" class="lybox-log__line">{{ log }}</div>
        </div>
      </section>
    </main>

    <!-- 底部调试面板按钮 -->
    <footer class="lybox-app__footer">
      <button class="lybox-btn lybox-btn--ghost" @click="toggleDebugPanel">
        {{ debugMounted ? '卸载调试面板' : '挂载调试面板' }}
      </button>
    </footer>
  </div>
</template>

<style scoped>
/* 全部样式使用 @lybox/sdk/css 的 CSS 变量，不硬编码颜色 */

.lybox-app {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  background-color: var(--lybox-color-background-0);
  color: var(--lybox-color-text-0);
  font-family: var(--lybox-font-family);
}

/* 顶部标题栏 */
.lybox-app__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--lybox-spacing-base) var(--lybox-spacing-loose);
  background-color: var(--lybox-color-background-1);
  border-bottom: 1px solid var(--lybox-color-border);
}

.lybox-app__title {
  display: flex;
  align-items: center;
  gap: var(--lybox-spacing-base);
}

.lybox-app__title h1 {
  margin: 0;
  font-size: var(--lybox-font-size-subtitle);
  font-weight: 600;
  color: var(--lybox-color-text-0);
}

/* 环境徽章 */
.lybox-app__badge {
  padding: 2px var(--lybox-spacing-extra-tight);
  border-radius: var(--lybox-radius-small);
  font-size: var(--lybox-font-size-caption);
  color: var(--lybox-color-background-1);
}

.lybox-app__badge--webview {
  background-color: var(--lybox-color-primary);
}

.lybox-app__badge--browser {
  background-color: var(--lybox-color-success);
}

/* 主体区域 */
.lybox-app__main {
  flex: 1;
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(360px, 1fr));
  gap: var(--lybox-spacing-base);
  padding: var(--lybox-spacing-loose);
}

/* 卡片 */
.lybox-card {
  padding: var(--lybox-spacing-base);
  background-color: var(--lybox-card-background);
  border: 1px solid var(--lybox-card-stroke);
  border-radius: var(--lybox-radius-medium);
  box-shadow: var(--lybox-shadow-card);
}

.lybox-card__title {
  margin: 0 0 var(--lybox-spacing-base);
  font-size: var(--lybox-font-size-subtitle);
  font-weight: 600;
  color: var(--lybox-color-text-0);
}

/* 行布局：标签 + 输入 + 按钮 */
.lybox-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: var(--lybox-spacing-tight);
  padding: var(--lybox-spacing-extra-tight) 0;
}

.lybox-row__label {
  min-width: 140px;
  font-size: var(--lybox-font-size-body);
  color: var(--lybox-color-text-1);
}

.lybox-row__operator {
  font-size: var(--lybox-font-size-body);
  color: var(--lybox-color-text-2);
}

.lybox-row__result {
  margin: var(--lybox-spacing-extra-tight) 0 0 140px;
  font-size: var(--lybox-font-size-body);
  color: var(--lybox-color-text-1);
  font-family: var(--lybox-font-family-mono);
}

/* 输入框 */
.lybox-input {
  padding: var(--lybox-spacing-extra-tight) var(--lybox-spacing-tight);
  font-size: var(--lybox-font-size-body);
  color: var(--lybox-color-text-0);
  background-color: var(--lybox-color-background-1);
  border: 1px solid var(--lybox-color-border);
  border-radius: var(--lybox-radius-small);
  outline: none;
  transition: border-color 0.15s ease;
}

.lybox-input:focus {
  border-color: var(--lybox-color-border-focus);
}

.lybox-input--num {
  width: 80px;
}

/* 按钮 */
.lybox-btn {
  padding: var(--lybox-spacing-extra-tight) var(--lybox-spacing-base);
  font-size: var(--lybox-font-size-body);
  border: 1px solid transparent;
  border-radius: var(--lybox-radius-small);
  cursor: pointer;
  transition: background-color 0.15s ease, border-color 0.15s ease;
}

.lybox-btn:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.lybox-btn--primary {
  color: var(--lybox-color-background-1);
  background-color: var(--lybox-color-primary);
}

.lybox-btn--primary:not(:disabled):hover {
  background-color: var(--lybox-color-primary-hover);
}

.lybox-btn--primary:not(:disabled):active {
  background-color: var(--lybox-color-primary-active);
}

.lybox-btn--ghost {
  color: var(--lybox-color-text-0);
  background-color: transparent;
  border-color: var(--lybox-color-border);
}

.lybox-btn--ghost:not(:disabled):hover {
  background-color: var(--lybox-subtle-hover);
}

/* 插件信息展示 */
.lybox-info {
  margin: var(--lybox-spacing-tight) 0 0 140px;
  padding: var(--lybox-spacing-tight) var(--lybox-spacing-base);
  background-color: var(--lybox-color-primary-light);
  border-radius: var(--lybox-radius-small);
}

.lybox-info__item {
  font-size: var(--lybox-font-size-body);
  color: var(--lybox-color-text-1);
  line-height: 1.6;
}

.lybox-info__key {
  color: var(--lybox-color-text-2);
}

/* 事件日志 */
.lybox-log {
  margin-top: var(--lybox-spacing-tight);
  max-height: 200px;
  overflow-y: auto;
  padding: var(--lybox-spacing-tight);
  background-color: var(--lybox-color-background-3);
  border-radius: var(--lybox-radius-small);
}

.lybox-log__empty {
  font-size: var(--lybox-font-size-caption);
  color: var(--lybox-color-text-3);
}

.lybox-log__line {
  padding: 2px 0;
  font-size: var(--lybox-font-size-caption);
  font-family: var(--lybox-font-family-mono);
  color: var(--lybox-color-text-1);
  border-bottom: 1px solid var(--lybox-color-border);
}

/* 底部 */
.lybox-app__footer {
  display: flex;
  justify-content: flex-end;
  padding: var(--lybox-spacing-base) var(--lybox-spacing-loose);
  background-color: var(--lybox-color-background-1);
  border-top: 1px solid var(--lybox-color-border);
}
</style>
