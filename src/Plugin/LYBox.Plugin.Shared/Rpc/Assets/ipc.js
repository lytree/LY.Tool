// LYBox WebView IPC 引导脚本（Wails v2 模型 + HTTP 传输回退）
// 由宿主经 ExecuteScriptAsync 注入页面（WebView 模式），或由 mock-server 直接 <script> 引入（浏览器模式）。
// 提供统一入口 window.__lybox.rpc(name, ...args): Promise<T>。
//
// 传输层自动检测环境：
//   - WebView 模式：invokeCSharpAction 存在 → 走原生 IPC（前缀字符 + JSON）
//   - 浏览器模式：invokeCSharpAction 不存在 → 走 HTTP（POST /__rpc）
//
// C# → JS 推送：
//   - WebView 模式：宿主执行 window.__lybox.resolve / dispatch
//   - 浏览器模式：复用 SSE（EventSource /sse/{pluginId}），dispatch 由 SSE 监听器触发
(function () {
  if (window.__lybox) return;

  var callbacks = new Map();             // callbackId -> {resolve, reject}
  var eventListeners = Object.create(null); // name -> Set<cb>
  var channelListeners = Object.create(null);// id -> Set<cb>

  // —— 传输层抽象 ——
  var isWebView = (typeof invokeCSharpAction === 'function');

  function send(body) {
    if (isWebView) {
      invokeCSharpAction(body);
    } else {
      // 浏览器模式：HTTP 桥接
      var prefix = body[0];
      var payload = body.substring(1);
      if (prefix === 'C') {
        httpRpc(payload);
      } else if (prefix === 'E') {
        // 事件 emit 走 POST /__emit（浏览器模式一般用不到，保留通道）
        fetch('/__emit', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: payload })['catch'](function (e) { console.error('[__lybox] HTTP emit 失败', e); });
      } else if (prefix === 'X') {
        fetch('/__channel/close', { method: 'POST', headers: { 'Content-Type': 'text/plain' }, body: payload })['catch'](function (e) { console.error('[__lybox] HTTP channel close 失败', e); });
      }
    }
  }

  // HTTP RPC：POST /__rpc，body = {name, args, callbackId}
  // 响应 {result: ...} 或 {error: "..."}
  function httpRpc(payload) {
    var msg;
    try { msg = JSON.parse(payload); } catch (e) { return; }
    fetch('/__rpc', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: msg.name, args: msg.args || [], callbackId: msg.callbackId })
    }).then(function (r) { return r.json(); })
      .then(function (r) {
        // 复用 resolve 机制回推 Promise
        resolve(msg.callbackId, r.error || null, r.result);
      })['catch'](function (e) {
        resolve(msg.callbackId, e.message, null);
      });
  }

  // JS → C#：发起 RPC 调用，返回 Promise。统一入口。
  function invoke(name, args) {
    return new Promise(function (resolve, reject) {
      var id = name + '-' + Date.now().toString(36) + '-' + Math.random().toString(36).slice(2, 10);
      callbacks.set(id, { resolve: resolve, reject: reject });
      send('C' + JSON.stringify({ name: name, args: args || [], callbackId: id }));
    });
  }

  // C# → JS：回传调用结果（err 为字符串则 reject；result 带 __channel 则包装为 Channel）。
  function resolve(id, err, result) {
    var cb = callbacks.get(id);
    if (!cb) return;
    callbacks.delete(id);
    if (err) {
      cb.reject(new Error(err));
    } else if (result && typeof result === 'object' && result.__channel) {
      cb.resolve(makeChannel(result.id, result.itemType));
    } else {
      cb.resolve(result);
    }
  }

  function makeChannel(id, itemType) {
    return {
      id: id,
      itemType: itemType,
      on: function (cb) {
        if (!channelListeners[id]) channelListeners[id] = new Set();
        channelListeners[id].add(cb);
        return function () { (channelListeners[id] || new Set()).delete(cb); };
      },
      close: function () { send('X' + id); }
    };
  }

  function channelOnData(id, data) {
    var set = channelListeners[id];
    if (set) set.forEach(function (cb) { try { cb(data); } catch (e) { console.error(e); } });
  }
  function channelOnClose(id) {
    var set = channelListeners[id];
    if (set) set.forEach(function (cb) { try { cb(null, true); } catch (e) { console.error(e); } });
    delete channelListeners[id];
  }

  // —— 事件系统（Wails EventsOn/Emit 语义）——
  function on(name, cb) {
    if (!eventListeners[name]) eventListeners[name] = new Set();
    eventListeners[name].add(cb);
    return function () { (eventListeners[name] || new Set()).delete(cb); };
  }
  function off(name, cb) { (eventListeners[name] || new Set()).delete(cb); }
  function emit(name, data) { send('E' + JSON.stringify({ name: name, data: data })); }
  function dispatch(name, data) {
    var set = eventListeners[name];
    if (set) set.forEach(function (cb) { try { cb(data); } catch (e) { console.error(e); } });
  }

  // 宿主注入绑定清单（WebView 模式）。浏览器模式由 mock-server 提供 /__lybox/bindings.json。
  // 当前实现为 noop：rpc 入口已统一为 __lybox.rpc(name, args)，无需构建 window.go。
  // manifest 仅供调试面板展示命令列表，不影响运行时调用。
  function setBindings(manifestJson) {
    window.__lyboxBindings = manifestJson; // 保留供调试工具读取
  }

  // —— SSE 监听器（C# → JS 主动推送）——
  // WebView 模式由宿主在 ipc.js 注入完成后显式调用 startSse(pluginId) 启动。
  // 浏览器模式自动启动（mock-server 或 Avalonia Kestrel 均提供 /sse/{pluginId}）。
  var sseStarted = false;
  function startSse(pluginId) {
    if (sseStarted) return;
    if (!pluginId || typeof EventSource === 'undefined') return;
    try {
      sseStarted = true;
      var es = new EventSource('/sse/' + encodeURIComponent(pluginId));
      es.addEventListener('dispatch', function (e) {
        try {
          var msg = JSON.parse(e.data);
          dispatch(msg.name, msg.data);
        } catch (err) { console.error('[__lybox] SSE dispatch 解析失败', err); }
      });
      es.addEventListener('channel-data', function (e) {
        try {
          var msg = JSON.parse(e.data);
          channelOnData(msg.id, msg.data);
        } catch (err) { console.error('[__lybox] SSE channel-data 解析失败', err); }
      });
      es.addEventListener('channel-close', function (e) {
        try {
          var msg = JSON.parse(e.data);
          channelOnClose(msg.id);
        } catch (err) { console.error('[__lybox] SSE channel-close 解析失败', err); }
      });
      es.onerror = function () { /* 浏览器会自动重连，无需处理 */ };
    } catch (e) {
      console.error('[__lybox] SSE 初始化失败', e);
      sseStarted = false;
    }
  }

  window.__lybox = {
    rpc: invoke,           // 统一 RPC 入口：rpc(name, ...args) => Promise<T>
    invoke: invoke,        // 别名（向后兼容）
    resolve: resolve,
    on: on,
    off: off,
    emit: emit,
    dispatch: dispatch,
    setBindings: setBindings,
    startSse: startSse,
    channel: { on: function (id, cb) { return makeChannel(id, '').on(cb); }, onData: channelOnData, onClose: channelOnClose },
    isWebView: isWebView   // 供前端检测当前环境
  };

  // 浏览器模式：自动启动 SSE（用 mock-plugin 作为默认 pluginId，可被后续 startSse 覆盖）
  if (!isWebView) {
    startSse('mock-plugin');
  }

  // 通知宿主运行时就绪（WebView 模式握手；浏览器模式无监听者，无副作用）。
  send('E' + JSON.stringify({ name: '__lybox:ready', data: null }));
})();
