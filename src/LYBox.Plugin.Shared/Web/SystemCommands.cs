using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using LYBox.Plugin.Shared.Rpc;
using Ursa.Controls;
using Ursa.Controls.Options;

namespace LYBox.Plugin.Shared.Web;

/// <summary>
/// 系统级 RPC 命令注册器：为每个 WebView 实例注册文件选择器与对话框命令。
/// 由 <see cref="WebPluginView"/> 在初始化时调用 <see cref="Register"/>。
/// 所有命令通过 <c>window.__lybox.rpc(name, ...args)</c> 调用。
/// </summary>
/// <remarks>
/// 命令清单：
/// <list type="table">
/// <listheader><term>命令名</term><description>说明</description></listheader>
/// <item><term>OpenFilePicker</term><description>打开文件选择器，返回路径数组</description></item>
/// <item><term>SaveFilePicker</term><description>打开保存文件对话框，返回路径或 null</description></item>
/// <item><term>OpenFolderPicker</term><description>打开文件夹选择器，返回路径数组</description></item>
/// <item><term>ShowMessageBox</term><description>显示消息框，返回按钮结果</description></item>
/// <item><term>ShowConfirmDialog</term><description>显示确认对话框，返回布尔值</description></item>
/// </list>
/// </remarks>
public static class SystemCommands
{
    /// <summary>
    /// 注册所有系统命令到指定的 RPC 主机。
    /// </summary>
    /// <param name="host">RPC 主机。</param>
    /// <param name="getTopLevel">获取 TopLevel 的委托（用于访问 StorageProvider）。</param>
    public static void Register(IRpcHost host, System.Func<TopLevel?> getTopLevel)
    {
        host.RegisterCommand("OpenFilePicker", (args, ct) => HandleOpenFilePickerAsync(args, getTopLevel, ct));
        host.RegisterCommand("SaveFilePicker", (args, ct) => HandleSaveFilePickerAsync(args, getTopLevel, ct));
        host.RegisterCommand("OpenFolderPicker", (args, ct) => HandleOpenFolderPickerAsync(args, getTopLevel, ct));
        host.RegisterCommand("ShowMessageBox", (args, ct) => HandleShowMessageBoxAsync(args, getTopLevel, ct));
        host.RegisterCommand("ShowConfirmDialog", (args, ct) => HandleShowConfirmDialogAsync(args, getTopLevel, ct));
    }

    // ==================== 文件选择器 ====================

    private static async Task<object?> HandleOpenFilePickerAsync(JsonElement[] args, System.Func<TopLevel?> getTopLevel, CancellationToken ct)
    {
        var topLevel = getTopLevel();
        if (topLevel?.StorageProvider is null)
            return new { error = "StorageProvider 不可用" };

        var options = ParseFileOpenOptions(args);
        var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);
        var paths = new List<string>();
        foreach (var f in result)
        {
            var p = f.TryGetLocalPath();
            if (p is not null) paths.Add(p);
        }
        return paths;
    }

    private static async Task<object?> HandleSaveFilePickerAsync(JsonElement[] args, System.Func<TopLevel?> getTopLevel, CancellationToken ct)
    {
        var topLevel = getTopLevel();
        if (topLevel?.StorageProvider is null)
            return new { error = "StorageProvider 不可用" };

        var options = ParseFileSaveOptions(args);
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(options);
        return file?.TryGetLocalPath();
    }

    private static async Task<object?> HandleOpenFolderPickerAsync(JsonElement[] args, System.Func<TopLevel?> getTopLevel, CancellationToken ct)
    {
        var topLevel = getTopLevel();
        if (topLevel?.StorageProvider is null)
            return new { error = "StorageProvider 不可用" };

        var options = ParseFolderPickerOptions(args);
        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
        var paths = new List<string>();
        foreach (var d in result)
        {
            var p = d.TryGetLocalPath();
            if (p is not null) paths.Add(p);
        }
        return paths;
    }

    // ==================== 对话框 ====================

    private static async Task<object?> HandleShowMessageBoxAsync(JsonElement[] args, System.Func<TopLevel?> getTopLevel, CancellationToken ct)
    {
        var opts = args.Length > 0 ? args[0] : default;
        var message = opts.ValueKind == JsonValueKind.Object && opts.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
        var title = opts.ValueKind == JsonValueKind.Object && opts.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
        var buttonStr = opts.ValueKind == JsonValueKind.Object && opts.TryGetProperty("button", out var b) ? b.GetString() ?? "OK" : "OK";
        var iconStr = opts.ValueKind == JsonValueKind.Object && opts.TryGetProperty("icon", out var i) ? i.GetString() ?? "info" : "info";

        var button = buttonStr?.ToLowerInvariant() switch
        {
            "yesno" => MessageBoxButton.YesNo,
            "yesnocancel" => MessageBoxButton.YesNoCancel,
            _ => MessageBoxButton.OK,
        };
        var icon = iconStr?.ToLowerInvariant() switch
        {
            "warning" => MessageBoxIcon.Warning,
            "error" => MessageBoxIcon.Error,
            "success" => MessageBoxIcon.Success,
            _ => MessageBoxIcon.None,
        };

        var result = await OverlayMessageBox.ShowAsync(message, title, button: button, icon: icon);
        return result.ToString();
    }

    private static async Task<object?> HandleShowConfirmDialogAsync(JsonElement[] args, System.Func<TopLevel?> getTopLevel, CancellationToken ct)
    {
        var opts = args.Length > 0 ? args[0] : default;
        var message = opts.ValueKind == JsonValueKind.Object && opts.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
        var title = opts.ValueKind == JsonValueKind.Object && opts.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
        var iconStr = opts.ValueKind == JsonValueKind.Object && opts.TryGetProperty("icon", out var i) ? i.GetString() ?? "warning" : "warning";

        var icon = iconStr?.ToLowerInvariant() switch
        {
            "info" => MessageBoxIcon.None,
            "error" => MessageBoxIcon.Error,
            "success" => MessageBoxIcon.Success,
            _ => MessageBoxIcon.Warning,
        };

        var result = await OverlayMessageBox.ShowAsync(message, title, button: MessageBoxButton.YesNo, icon: icon);
        return result == MessageBoxResult.Yes;
    }

    // ==================== 选项解析 ====================

    private static FilePickerOpenOptions ParseFileOpenOptions(JsonElement[] args)
    {
        var opts = new FilePickerOpenOptions();
        if (args.Length == 0 || args[0].ValueKind != JsonValueKind.Object) return opts;
        var obj = args[0];

        if (obj.TryGetProperty("title", out var title))
            opts.Title = title.GetString();

        if (obj.TryGetProperty("multiple", out var multiple) && multiple.GetBoolean())
            opts.AllowMultiple = true;

        if (obj.TryGetProperty("filters", out var filtersEl) && filtersEl.ValueKind == JsonValueKind.Array)
        {
            var fileTypes = new List<FilePickerFileType>();
            foreach (var f in filtersEl.EnumerateArray())
            {
                var name = f.TryGetProperty("name", out var n) ? n.GetString() ?? "Filter" : "Filter";
                var exts = new List<string>();
                if (f.TryGetProperty("extensions", out var extEl) && extEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var e in extEl.EnumerateArray())
                    {
                        var ext = e.GetString();
                        if (!string.IsNullOrEmpty(ext)) exts.Add(ext);
                    }
                }
                fileTypes.Add(new FilePickerFileType(name) { Patterns = exts.ToArray() });
            }
            opts.FileTypeFilter = fileTypes;
        }

        return opts;
    }

    private static FilePickerSaveOptions ParseFileSaveOptions(JsonElement[] args)
    {
        var opts = new FilePickerSaveOptions();
        if (args.Length == 0 || args[0].ValueKind != JsonValueKind.Object) return opts;
        var obj = args[0];

        if (obj.TryGetProperty("title", out var title))
            opts.Title = title.GetString();

        if (obj.TryGetProperty("suggestedFileName", out var sfn))
            opts.SuggestedFileName = sfn.GetString();

        if (obj.TryGetProperty("filters", out var filtersEl) && filtersEl.ValueKind == JsonValueKind.Array)
        {
            var fileTypes = new List<FilePickerFileType>();
            foreach (var f in filtersEl.EnumerateArray())
            {
                var name = f.TryGetProperty("name", out var n) ? n.GetString() ?? "Filter" : "Filter";
                var exts = new List<string>();
                if (f.TryGetProperty("extensions", out var extEl) && extEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var e in extEl.EnumerateArray())
                    {
                        var ext = e.GetString();
                        if (!string.IsNullOrEmpty(ext)) exts.Add(ext);
                    }
                }
                fileTypes.Add(new FilePickerFileType(name) { Patterns = exts.ToArray() });
            }
            opts.FileTypeChoices = fileTypes;
        }

        return opts;
    }

    private static FolderPickerOpenOptions ParseFolderPickerOptions(JsonElement[] args)
    {
        var opts = new FolderPickerOpenOptions();
        if (args.Length == 0 || args[0].ValueKind != JsonValueKind.Object) return opts;
        var obj = args[0];

        if (obj.TryGetProperty("title", out var title))
            opts.Title = title.GetString();

        if (obj.TryGetProperty("multiple", out var multiple) && multiple.GetBoolean())
            opts.AllowMultiple = true;

        return opts;
    }
}
