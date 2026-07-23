using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using LYBox.Plugin.Shared.Rpc;
using LYBox.Plugin.Shared.Services;

namespace LYBox.Plugin.Shared.Web;

/// <summary>
/// 封装 <see cref="WebView"/> 的 UserControl，提供 Web 插件的页面承载 + IPC 集成。
/// </summary>
/// <remarks>
/// <para>
/// 使用方式：在插件页面 XAML 中放置 <c>&lt;web:WebPluginView PluginId="{Binding PluginId}" /&gt;</c>。
/// 控件附加到视觉树后自动完成：
/// <list type="number">
/// <item>从 <see cref="ServiceLocator"/> 获取 <see cref="WebHostService"/> 单例。</item>
/// <item>构造 <see cref="WebViewIpcTransport"/>（桥接 WebView 原生 IPC）+ <see cref="WebViewIpcHost"/>（注入 SSE 推送器）。</item>
/// <item>导航到 <c>{BaseUrl}/{PluginId}/index.html</c>。</item>
/// <item><see cref="WebView.NavigationCompleted"/> 后注入 ipc.js → 调用 <c>startSse(pluginId)</c> → 注册 [RpcCommand] 绑定 → 注入绑定清单。</item>
/// </list>
/// </para>
/// <para>
/// <see cref="RpcHost"/> 属性在初始化完成后暴露创建的 <see cref="WebViewIpcHost"/>，
/// 供外部（如插件 ViewModel）调用 <see cref="IRpcHost.EmitEventAsync"/> 主动推送事件。
/// </para>
/// </remarks>
public partial class WebPluginView : UserControl
{
    private WebViewIpcTransport? _transport;
    private WebViewIpcHost? _host;
    private bool _initialized;
    private bool _isAttached;

    public static readonly StyledProperty<string?> PluginIdProperty =
        AvaloniaProperty.Register<WebPluginView, string?>(nameof(PluginId));

    static WebPluginView()
    {
        PluginIdProperty.Changed.AddClassHandler<WebPluginView>((v, e) => v.OnPluginIdChanged(e));
    }

    public WebPluginView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 插件 ID（Kestrel 路由前缀 + SSE 通道 key）。
    /// 必须在控件附加到视觉树前或后通过 XAML 绑定设置。
    /// </summary>
    public string? PluginId
    {
        get => GetValue(PluginIdProperty);
        set => SetValue(PluginIdProperty, value);
    }

    /// <summary>
    /// 初始化完成后创建的 RPC 主机。供外部调用 <see cref="IRpcHost.EmitEventAsync"/> 等方法。
    /// </summary>
    public IRpcHost? RpcHost => _host;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = true;
        base.OnAttachedToVisualTree(e);
        TryInitialize();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        _transport?.Detach();
        _transport = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnPluginIdChanged(AvaloniaPropertyChangedEventArgs e)
    {
        // 绑定可能在控件附加后生效：此时若已附加且未初始化，触发初始化
        if (_isAttached && !_initialized && e.NewValue is string pid && !string.IsNullOrEmpty(pid))
        {
            _ = TryInitialize();
        }
    }

    private async Task TryInitialize()
    {
        if (_initialized) return;
        var pluginId = PluginId;
        if (string.IsNullOrEmpty(pluginId)) return;
        _initialized = true;

        // Linux WPE WebKit 后端为实验性（EGL 支持未完成，issue #14 open）
        if (OperatingSystem.IsLinux())
        {
            System.Diagnostics.Debug.WriteLine(
                "[WebPluginView] 警告：Linux WPE WebKit 后端为实验性，嵌入式 WebView 可能不稳定。" +
                "如遇渲染问题，可降级为 NativeWebDialog（WebKitGTK 独立窗口）。");
        }

        var webView = this.FindControl<NativeWebView>("PART_WebView");
        if (webView is null) return;

        // 1. 获取 WebHostService 单例
        if (!ServiceLocator.TryGetService<WebHostService>(out var webHost) || webHost is null)
            return;

        // 2. 构造 IPC 传输层 + 主机（注入 SSE pusher + pluginId + webHost 以启用 SSE 推送 + HTTP RPC 桥）
        _transport = new WebViewIpcTransport(webView);
        _host = new WebViewIpcHost(_transport, webHost.EventPusher, pluginId, webHost);

        // 3. 注册插件的 [RpcCommand] 绑定（需在 InjectBindingsAsync 前完成）
        RegisterPluginBindings(pluginId);

        // 3b. 注册系统级命令（文件选择器 + 对话框），所有 web 插件共享
        SystemCommands.Register(_host, () => TopLevel.GetTopLevel(this));

        // 4. 订阅 NavigationCompleted 注入引导脚本
        webView.NavigationCompleted += async (_, _) =>
        {
            try
            {
                // 4a. 注入 ipc.js（含 __lybox 运行时 + startSse 函数）
                await _host.InitializeAsync().ConfigureAwait(false);
                // 4b. 显式启动 SSE（pluginId 由参数传入，非全局变量）
                var pidJson = JsonSerializer.Serialize(pluginId);
                await _transport.ExecuteScriptAsync(
                    $"window.__lybox && window.__lybox.startSse({pidJson});").ConfigureAwait(false);
                // 4c. 注入绑定清单（window.go.* 胶水）
                await _host.InjectBindingsAsync().ConfigureAwait(false);
            }
            catch
            {
                // 页面已销毁或 WebView 未就绪，忽略
            }
        };

        // 5. 导航到插件入口页
        var url = $"{webHost.BaseUrl}/{pluginId}/index.html";
        webView.Source = new Uri(url);
    }

    private void RegisterPluginBindings(string pluginId)
    {
        if (_host is null) return;
        if (!ServiceLocator.TryGetService<IPluginLoader>(out var loader) || loader is null) return;

        var plugin = loader.GetLoadedPlugin(pluginId) as IWebPlugin;
        if (plugin is null) return;

        WebPluginBindings.Register(_host, plugin);
    }
}
