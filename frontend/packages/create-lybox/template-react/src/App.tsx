import { useState, useEffect, useCallback, useRef } from 'react';
import { createRpcClient, on, isWebView, mountDebugPanel, setTheme, getTheme } from '@lytree/sdk';
import type { RpcMethodDefinition } from '@lytree/sdk';
import '@lytree/sdk/components';
import './App.css';

// SSE 推送事件的数据结构
interface TickEvent {
  count: number;
  time: string;
  message: string;
}

// GetPluginInfoAsync 返回的插件信息
interface PluginInfo {
  id: string;
  name: string;
  version: string;
}

// 声明宿主 RPC 方法表：命令名 → （参数类型，返回类型）。
// createRpcClient 依据此表生成类型安全的客户端，编译期即校验命令名与参数。
interface DemoCommands extends Record<string, RpcMethodDefinition> {
  GreetAsync: { args: [string]; result: string };
  AddAsync: { args: [number, number]; result: number };
  GetPluginInfoAsync: { args: []; result: PluginInfo };
  OpenFilePicker: { args: [object]; result: string[] };
  ShowMessageBox: { args: [object]; result: string };
}

// 类型安全的 RPC 客户端（SDK 封装：命令名/参数/返回值全部类型化）
const demo = createRpcClient<DemoCommands>();

function App() {
  // 当前主题状态
  const [theme, setThemeState] = useState<'light' | 'dark'>(() => getTheme());

  // RPC 调用相关状态
  const [greetName, setGreetName] = useState('World');
  const [greetResult, setGreetResult] = useState<string>('');
  const [greetLoading, setGreetLoading] = useState(false);

  const [addA, setAddA] = useState('3');
  const [addB, setAddB] = useState('5');
  const [addResult, setAddResult] = useState<string>('');
  const [addLoading, setAddLoading] = useState(false);

  const [pluginInfo, setPluginInfo] = useState<PluginInfo | null>(null);
  const [infoLoading, setInfoLoading] = useState(false);

  // SSE 事件相关状态
  const [tickCount, setTickCount] = useState(0);
  const [tickLogs, setTickLogs] = useState<TickEvent[]>([]);

  // 是否运行在 WebView 宿主中
  const [webViewMode] = useState(() => isWebView());

  // 调试面板挂载状态
  const [debugMounted, setDebugMounted] = useState(false);
  const unmountDebugRef = useRef<(() => void) | null>(null);

  // 主题切换
  const handleToggleTheme = useCallback(() => {
    const next = theme === 'light' ? 'dark' : 'light';
    setTheme(next);
    setThemeState(next);
  }, [theme]);

  // 调用 GreetAsync（参数为名字字符串）
  const handleGreet = useCallback(async () => {
    setGreetLoading(true);
    setGreetResult('');
    try {
      const prefix = await demo.invoke('GreetAsync', greetName);
      setGreetResult(`${prefix}${greetName}`);
    } catch (err) {
      setGreetResult(`错误：${(err as Error).message}`);
    } finally {
      setGreetLoading(false);
    }
  }, [greetName]);

  // 调用 AddAsync（参数为两个数字）
  const handleAdd = useCallback(async () => {
    setAddLoading(true);
    setAddResult('');
    try {
      const a = Number(addA);
      const b = Number(addB);
      const result = await demo.invoke('AddAsync', a, b);
      setAddResult(`${a} + ${b} = ${result}`);
    } catch (err) {
      setAddResult(`错误：${(err as Error).message}`);
    } finally {
      setAddLoading(false);
    }
  }, [addA, addB]);

  // 调用 GetPluginInfoAsync（无参数）
  const handleGetInfo = useCallback(async () => {
    setInfoLoading(true);
    setPluginInfo(null);
    try {
      const result = await demo.invoke('GetPluginInfoAsync');
      setPluginInfo(result);
    } catch (err) {
      setPluginInfo({ id: '错误', name: (err as Error).message, version: '-' });
    } finally {
      setInfoLoading(false);
    }
  }, []);

  // 订阅 SSE tick 事件
  useEffect(() => {
    const off = on<TickEvent>('tick', (data) => {
      if (!data) return;
      setTickCount((c) => c + 1);
      setTickLogs((logs) => {
        const next = [data, ...logs];
        return next.slice(0, 20);
      });
    });
    return () => {
      if (typeof off === 'function') off();
    };
  }, []);

  // 卸载时清理调试面板
  useEffect(() => {
    return () => {
      if (unmountDebugRef.current) {
        unmountDebugRef.current();
        unmountDebugRef.current = null;
      }
    };
  }, []);

  // 切换调试面板挂载状态
  const handleToggleDebug = useCallback(() => {
    if (debugMounted) {
      if (unmountDebugRef.current) {
        unmountDebugRef.current();
        unmountDebugRef.current = null;
      }
      setDebugMounted(false);
    } else {
      unmountDebugRef.current = mountDebugPanel({ defaultOpen: true });
      setDebugMounted(true);
    }
  }, [debugMounted]);

  return (
    <div className="app-container">
      {/* 顶部标题栏 */}
      <header className="header">
        <div className="header-title">
          <h1 className="header-name">LYBox React 插件示例</h1>
          <span className={`ly-badge ${webViewMode ? 'ly-badge-primary' : 'ly-badge-success'}`}>
            {webViewMode ? 'WebView IPC' : 'Browser (Mock)'}
          </span>
        </div>
        <button className="ly-btn ly-btn-secondary" onClick={handleToggleTheme}>
          {theme === 'dark' ? '🌙 深色' : '☀️ 浅色'}
        </button>
      </header>

      <main className="main">
        {/* RPC 调用区 */}
        <section className="ly-card">
          <h2 className="card-title">RPC 调用</h2>

          {/* GreetAsync */}
          <div className="row">
            <label className="row-label">GreetAsync</label>
            <input
              className="ly-input input-narrow"
              type="text"
              value={greetName}
              onChange={(e) => setGreetName(e.target.value)}
              placeholder="输入名字"
            />
            <button className="ly-btn ly-btn-primary" onClick={handleGreet} disabled={greetLoading}>
              {greetLoading ? '调用中…' : '调用'}
            </button>
          </div>
          {greetResult && <div className="result">结果：{greetResult}</div>}

          {/* AddAsync */}
          <div className="row">
            <label className="row-label">AddAsync</label>
            <input
              className="ly-input input-narrow"
              type="number"
              value={addA}
              onChange={(e) => setAddA(e.target.value)}
            />
            <span className="row-operator">+</span>
            <input
              className="ly-input input-narrow"
              type="number"
              value={addB}
              onChange={(e) => setAddB(e.target.value)}
            />
            <button className="ly-btn ly-btn-primary" onClick={handleAdd} disabled={addLoading}>
              {addLoading ? '调用中…' : '调用'}
            </button>
          </div>
          {addResult && <div className="result">结果：{addResult}</div>}

          {/* GetPluginInfoAsync */}
          <div className="row">
            <label className="row-label">GetPluginInfoAsync</label>
            <button className="ly-btn ly-btn-primary" onClick={handleGetInfo} disabled={infoLoading}>
              {infoLoading ? '调用中…' : '获取插件信息'}
            </button>
          </div>
          {pluginInfo && (
            <div className="result result-block">
              <div><span className="result-key">ID</span>：{pluginInfo.id}</div>
              <div><span className="result-key">名称</span>：{pluginInfo.name}</div>
              <div><span className="result-key">版本</span>：{pluginInfo.version}</div>
            </div>
          )}
        </section>

        {/* SSE 事件区 */}
        <section className="ly-card">
          <h2 className="card-title">
            SSE 事件订阅（tick）
            <span className="ly-badge ly-badge-info">已收到 {tickCount}</span>
          </h2>
          <p className="card-hint">
            已订阅 <code>tick</code> 事件，每 2 秒由宿主或 Mock 后端推送一次。
          </p>
          <div className="log">
            {tickLogs.length === 0 ? (
              <div className="log-empty">（等待事件推送…）</div>
            ) : (
              tickLogs.map((log, idx) => (
                <div className="log-line" key={`${log.time}-${idx}`}>
                  <span className="log-time">[{log.time}]</span>
                  <span className="log-count">#{log.count}</span>
                  <span className="log-message">{log.message}</span>
                </div>
              ))
            )}
          </div>
        </section>

        {/* 调试面板 */}
        <section className="ly-card">
          <h2 className="card-title">调试工具</h2>
          <p className="card-hint">
            调试面板提供 RPC 方法枚举、事件监听、日志查看等能力，方便开发期排障。
          </p>
          <button className="ly-btn ly-btn-secondary" onClick={handleToggleDebug}>
            {debugMounted ? '卸载调试面板' : '挂载调试面板'}
          </button>
        </section>
      </main>

      <footer className="footer">
        <span>LYBox React Plugin Template · 与宿主 Fluent Design 主题一致</span>
      </footer>
    </div>
  );
}

export default App;