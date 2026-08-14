using System.Collections.Generic;
using System.Text;

namespace LYBox.Plugin.Shared.Web;

/// <summary>
/// 调试面板 HTML 生成器。GET /__lybox/debug 端点返回此 HTML。
/// 功能：列出所有已注册 RPC 命令、参数表单、一键调用、SSE 事件流实时查看。
/// 仅 Debug 配置启用（由 WebHostService 的 #if DEBUG 保护）。
/// </summary>
internal static class DebugPanelHtml
{
    public static string Render(IEnumerable<string> commands)
    {
        var cmdArray = new List<string>(commands);
        var cmdJson = System.Text.Json.JsonSerializer.Serialize(cmdArray);

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"zh-CN\"><head>");
        sb.Append("<meta charset=\"UTF-8\"/>");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"/>");
        sb.Append("<title>LYBox Debug Panel</title>");
        sb.Append("<style>");
        sb.Append("* { box-sizing: border-box; margin: 0; padding: 0; }");
        sb.Append("body { font-family: -apple-system, 'Segoe UI', Roboto, sans-serif; padding: 24px; background: #fafafa; color: #1a1a1a; }");
        sb.Append("h1 { font-size: 22px; margin-bottom: 16px; }");
        sb.Append("h2 { font-size: 16px; margin: 16px 0 8px; color: #444; }");
        sb.Append(".card { background: #fff; border: 1px solid #e0e0e0; border-radius: 8px; padding: 16px; margin-bottom: 16px; }");
        sb.Append(".cmd-row { padding: 8px; border-bottom: 1px solid #eee; }");
        sb.Append(".cmd-name { font-family: monospace; color: #0078d4; font-weight: 600; }");
        sb.Append("input { padding: 4px 8px; border: 1px solid #ccc; border-radius: 4px; font-size: 13px; margin: 2px; }");
        sb.Append("button { padding: 4px 12px; background: #0078d4; color: #fff; border: none; border-radius: 4px; cursor: pointer; font-size: 13px; }");
        sb.Append("button:hover { background: #106ebe; }");
        sb.Append(".result { margin-top: 4px; padding: 6px; background: #f0f6ff; border-radius: 4px; font-family: monospace; font-size: 12px; min-height: 16px; word-break: break-all; }");
        sb.Append("#sse-log { max-height: 300px; overflow-y: auto; font-size: 12px; font-family: monospace; }");
        sb.Append(".sse-entry { padding: 2px 0; border-bottom: 1px solid #eee; }");
        sb.Append(".badge { display: inline-block; padding: 2px 8px; background: #0078d4; color: #fff; border-radius: 10px; font-size: 11px; }");
        sb.Append("</style></head><body>");

        sb.Append("<h1>LYBox Debug Panel <span class=\"badge\">DEBUG</span></h1>");
        sb.Append("<p>WebView RPC 命令清单与 SSE 事件流调试器。仅 Debug 配置可用。</p>");

        // RPC 命令列表
        sb.Append("<div class=\"card\">");
        sb.Append("<h2>RPC 命令（").Append(cmdArray.Count).Append(" 个）</h2>");
        sb.Append("<div id=\"cmd-list\"></div>");
        sb.Append("</div>");

        // SSE 事件流
        sb.Append("<div class=\"card\">");
        sb.Append("<h2>SSE 事件流</h2>");
        sb.Append("<p>连接 <code>/sse/{pluginId}</code> 接收 C# 主动推送：</p>");
        sb.Append("<div style=\"margin:8px 0\">");
        sb.Append("<input type=\"text\" id=\"sse-plugin-id\" placeholder=\"pluginId\" value=\"\" size=\"40\"/>");
        sb.Append("<button onclick=\"startSse()\">连接 SSE</button>");
        sb.Append("<button onclick=\"stopSse()\" style=\"background:#666\">断开</button>");
        sb.Append("</div>");
        sb.Append("<div id=\"sse-log\" style=\"background:#f9f9f9;padding:8px;border-radius:4px;\">");
        sb.Append("<div class=\"sse-entry\" style=\"color:#999;\">（等待连接...）</div>");
        sb.Append("</div>");
        sb.Append("</div>");

        sb.Append("<script>");
        sb.Append("var COMMANDS = ").Append(cmdJson).Append(";");

        // 渲染命令列表
        sb.Append(@"
            function renderCommands() {
                var list = document.getElementById('cmd-list');
                list.innerHTML = '';
                COMMANDS.forEach(function (name, idx) {
                    var row = document.createElement('div');
                    row.className = 'cmd-row';
                    row.innerHTML = '<div><span class=""cmd-name"">' + name + '</span></div>' +
                        '<div style=""margin:4px 0"">' +
                        '<input type=""text"" id=""args-' + idx + '"" placeholder=""参数（JSON 数组，如 [""World""] 或 [3, 5]）"" size=""60""/>' +
                        '<button onclick=""callCmd(' + idx + ')"">调用</button>' +
                        '</div>' +
                        '<div class=""result"" id=""result-' + idx + '"">（未调用）</div>';
                    list.appendChild(row);
                });
            }
        ");

        // 调用 RPC
        sb.Append(@"
            async function callCmd(idx) {
                var name = COMMANDS[idx];
                var argsStr = document.getElementById('args-' + idx).value || '[]';
                var args;
                try { args = JSON.parse(argsStr); } catch (e) {
                    document.getElementById('result-' + idx).textContent = '参数解析失败: ' + e.message;
                    return;
                }
                document.getElementById('result-' + idx).textContent = '调用中...';
                try {
                    var resp = await fetch('/__rpc', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ name: name, args: args })
                    });
                    var r = await resp.json();
                    document.getElementById('result-' + idx).textContent = JSON.stringify(r, null, 2);
                } catch (e) {
                    document.getElementById('result-' + idx).textContent = '错误: ' + e.message;
                }
            }
        ");

        // SSE 连接
        sb.Append(@"
            var sseEs = null;
            function startSse() {
                if (sseEs) { sseEs.close(); sseEs = null; }
                var pid = document.getElementById('sse-plugin-id').value;
                if (!pid) { alert('请输入 pluginId'); return; }
                sseEs = new EventSource('/sse/' + encodeURIComponent(pid));
                sseEs.addEventListener('dispatch', function (e) {
                    appendSse(e.data);
                });
                sseEs.addEventListener('ready', function (e) {
                    appendSse('[ready] ' + e.data);
                });
                sseEs.onerror = function () { appendSse('[error] 连接异常或已断开'); };
                appendSse('[连接中] /sse/' + pid);
            }
            function stopSse() {
                if (sseEs) { sseEs.close(); sseEs = null; appendSse('[已断开]'); }
            }
            function appendSse(data) {
                var log = document.getElementById('sse-log');
                var entry = document.createElement('div');
                entry.className = 'sse-entry';
                entry.textContent = new Date().toLocaleTimeString() + ' ' + data;
                log.insertBefore(entry, log.firstChild);
            }
        ");

        sb.Append("renderCommands();");
        sb.Append("</script></body></html>");

        return sb.ToString();
    }
}
