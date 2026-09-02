import assert from "node:assert/strict";
import test from "node:test";

const sdkPath = "../../src/Plugin/LYBox.Plugin.Shared.Web/Assets/lybox-plugin-sdk.js";

function createWindow() {
  const listeners = new Map();
  return {
    addEventListener(name, handler) {
      const handlers = listeners.get(name) ?? [];
      handlers.push(handler);
      listeners.set(name, handlers);
    },
    dispatchEvent(event) {
      for (const handler of listeners.get(event.type) ?? []) handler(event);
    },
  };
}

test("browser SDK forwards variadic RPC arguments to the native bridge", async () => {
  const calls = [];
  globalThis.window = createWindow();
  window.__lybox = {
    rpc(method, ...args) {
      calls.push({ method, args });
      return Promise.resolve(args.reduce((sum, value) => sum + value, 0));
    },
  };

  const sdk = await import(`${sdkPath}?native=${Date.now()}`);
  assert.equal(await sdk.lyboxInvoke("AddAsync", 3, 5), 8);
  assert.deepEqual(calls, [{ method: "AddAsync", args: [3, 5] }]);
  assert.equal(sdk.invoke, sdk.lyboxInvoke);

  delete globalThis.window;
});

test("browser SDK forwards variadic RPC arguments to local mocks", async () => {
  globalThis.window = createWindow();
  const sdk = await import(`${sdkPath}?mock=${Date.now()}`);
  sdk.registerLyboxMocks({
    AddAsync: (a, b) => a + b,
  });

  assert.equal(await sdk.lyboxInvoke("AddAsync", 4, 7), 11);
  delete globalThis.window;
});

test("late native bridge replaces EventSource and receives SDK events", async () => {
  const sources = [];
  globalThis.EventSource = class {
    constructor(url) {
      this.url = url;
      this.closed = false;
      this.listeners = new Map();
      sources.push(this);
    }

    addEventListener(name, handler) {
      this.listeners.set(name, handler);
    }

    close() {
      this.closed = true;
    }
  };
  globalThis.window = createWindow();

  const sdk = await import(`${sdkPath}?events=${Date.now()}`);
  sdk.installLyboxRuntime({
    runtimeVersion: "1",
    pluginKey: "sample.plugin",
    pageId: "home",
    sseUrl: "http://127.0.0.1/sse/sample.plugin",
  });

  const bridgeListeners = new Map();
  const bridgeUnsubscribed = [];
  window.__lybox = {
    rpc() {},
    on(name, handler) {
      bridgeListeners.set(name, handler);
      return () => bridgeUnsubscribed.push(name);
    },
  };

  const received = [];
  const unsubscribe = sdk.lyboxOn("tick", value => received.push(value));
  window.dispatchEvent({ type: "lybox:bridge-ready" });

  assert.equal(sources.length, 1);
  assert.equal(sources[0].closed, true);
  assert.equal(typeof bridgeListeners.get("tick"), "function");

  bridgeListeners.get("tick")({ count: 1 });
  unsubscribe();
  assert.deepEqual(received, [{ count: 1 }]);
  assert.deepEqual(bridgeUnsubscribed, ["tick"]);

  delete globalThis.EventSource;
  delete globalThis.window;
});
