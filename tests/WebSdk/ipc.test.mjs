import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import vm from "node:vm";

const scriptPath = new URL("../../src/Plugin/LYBox.Plugin.Shared.Web/Rpc/Assets/ipc.js", import.meta.url);

test("ipc bootstrap emits variadic RPC arguments and bridge ready event", async () => {
  const messages = [];
  const events = [];
  const script = await readFile(scriptPath, "utf8");
  const context = {
    console,
    CustomEvent: class {
      constructor(type, options) {
        this.type = type;
        this.detail = options?.detail;
      }
    },
    invokeCSharpAction(message) {
      messages.push(message);
    },
    window: {
      dispatchEvent(event) {
        events.push(event);
      },
    },
  };

  vm.runInNewContext(script, context);
  context.window.__lybox.rpc("AddAsync", 3, 5);

  const call = messages.find(message => message.startsWith("C"));
  assert.ok(call);
  assert.equal(JSON.stringify(JSON.parse(call.slice(1)).args), "[3,5]");
  assert.equal(events.filter(event => event.type === "lybox:bridge-ready").length, 1);
  assert.equal(events[0].detail, context.window.__lybox);
});
