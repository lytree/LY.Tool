using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using LYBox.Plugin.Shared.Rpc;
using LYBox.Plugin.Shared.Services;

namespace LYBox.Plugin.Shared.Web;

/// <summary>
/// 封装 <see cref="WebView"/> 的 UserControl，提供 Web 插件的页面承载 + IPC 集成 + 开发展示层。
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
/// <para>
/// 展示层（自公司版 PluginWebViewPage 移植）：
/// 设置 <see cref="ShowDevelopmentToolbar"/> 为 <c>true</c> 后显示开发工具栏
/// （Back/Forward/Refresh + Route/Status），并对导航执行授权域校验：
/// 越权导航会被拦截并在开发模式下展示错误页（可 Retry）。
/// </para>
/// </remarks>
public partial class WebPluginView : UserControl
{
    private static readonly Uri ErrorPageBaseUri = new("about:blank");

    private WebViewIpcTransport? _transport;
    private WebViewIpcHost? _host;
    private NativeWebView? _webView;
    private bool _initialized;
    private bool _isAttached;

    // ---- 展示层状态（移植自 PluginWebViewPage） ----
    private Border? _devToolbar;
    private Button? _backButton;
    private Button? _forwardButton;
    private TextBlock? _modeText;
    private TextBlock? _routeText;
    private TextBlock? _statusText;
    private Uri? _authorizedBaseUri;
    private Uri? _targetUri;
    private string _routeBasePath = string.Empty;
    private bool _isErrorPageActive;

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
    /// 是否显示开发工具栏（Back/Forward/Refresh + Route/Status）并启用导航信任校验与开发错误页。
    /// 默认关闭；调试 Web 插件页面时可全局开启。
    /// </summary>
    public static bool ShowDevelopmentToolbar { get; set; }

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
        if (_webView is not null)
        {
            _webView.NavigationStarted -= OnNavigationStarted;
            _webView.WebMessageReceived -= OnDevWebMessageReceived;
        }

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
        _webView = webView;

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

        // 3c. 展示层：开发工具栏 + 导航信任校验 + 开发错误页
        InitializeDevTools(webView, webHost.BaseUrl, pluginId);

        // 4. 订阅 NavigationCompleted 注入引导脚本
        webView.NavigationCompleted += async (_, args) =>
        {
            try
            {
                UpdateDevStatus(args.IsSuccess ? "Loaded" : "Failed", args);
                if (!args.IsSuccess)
                {
                    HandleNavigationFailure(args.Request, "Navigation failed.");
                    return;
                }

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
        _targetUri = new Uri(url);
        webView.Source = _targetUri;
    }

    private void RegisterPluginBindings(string pluginId)
    {
        if (_host is null) return;
        if (!ServiceLocator.TryGetService<IPluginLoader>(out var loader) || loader is null) return;

        var plugin = loader.GetLoadedPlugin(pluginId) as IWebPlugin;
        if (plugin is null) return;

        WebPluginBindings.Register(_host, plugin);
    }

    // ==================== 展示层（移植自 PluginWebViewPage） ====================

    private void InitializeDevTools(NativeWebView webView, string baseUrl, string pluginId)
    {
        _authorizedBaseUri = new Uri(baseUrl);

        if (ShowDevelopmentToolbar)
        {
            _devToolbar = this.FindControl<Border>("PART_DevToolbar");
            _backButton = this.FindControl<Button>("PART_BackButton");
            _forwardButton = this.FindControl<Button>("PART_ForwardButton");
            _modeText = this.FindControl<TextBlock>("PART_ModeText");
            _routeText = this.FindControl<TextBlock>("PART_RouteText");
            _statusText = this.FindControl<TextBlock>("PART_StatusText");

            if (_devToolbar is not null)
                _devToolbar.IsVisible = true;
            if (_modeText is not null)
                _modeText.Text = "DEV";
            if (_statusText is not null)
                _statusText.Text = "Idle";

            if (_targetUri is not null)
                _routeBasePath = PluginWebViewDevTools.GetRouteBasePath(_targetUri, $"/{pluginId}/index.html");
            if (_routeText is not null && _targetUri is not null)
                _routeText.Text = PluginWebViewDevTools.GetRouteText(_targetUri, _routeBasePath);
        }

        // 导航信任校验在开发模式下启用；生产模式同样拦截越权导航但直接取消（不显示错误页）
        webView.NavigationStarted += OnNavigationStarted;

        // 错误页 Retry 消息（JS invokeCSharpAction）与 Rpc 传输共用 WebMessageReceived，多订阅者互不影响
        webView.WebMessageReceived += OnDevWebMessageReceived;
    }

    private void OnNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        var request = e.Request;
        if (_isErrorPageActive)
        {
            if (!request.Equals(ErrorPageBaseUri))
                e.Cancel = true;
            return;
        }

        if (_authorizedBaseUri is null)
            return;

        if (!PluginWebViewDevTools.IsAllowedNavigation(request, _authorizedBaseUri))
        {
            e.Cancel = true;
            HandleNavigationFailure(request, "Navigation URL is outside this plugin's authorized namespace.");
            return;
        }

        if (_devToolbar is not null && _statusText is not null)
        {
            _statusText.Text = "Loading";
            if (_routeText is not null)
                _routeText.Text = PluginWebViewDevTools.GetRouteText(request, _routeBasePath);
        }
    }

    private void OnDevWebMessageReceived(object? sender, WebMessageReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Body))
            return;

        try
        {
            using var doc = JsonDocument.Parse(e.Body);
            if (doc.RootElement.TryGetProperty("kind", out var kind)
                && kind.ValueKind == JsonValueKind.String
                && kind.GetString() == PluginWebViewDevTools.RetryMessageKind)
            {
                RetryTarget();
            }
        }
        catch (JsonException)
        {
            // 非重试消息（如 Rpc 信封），交由 Rpc 传输层处理
        }
    }

    private void UpdateDevStatus(string status, WebViewNavigationCompletedEventArgs args)
    {
        if (_statusText is null)
            return;

        _statusText.Text = status;
        if (_routeText is not null)
            _routeText.Text = PluginWebViewDevTools.GetRouteText(args.Request, _routeBasePath);

        if (_backButton is not null && _webView is not null)
            _backButton.IsEnabled = _webView.CanGoBack;
        if (_forwardButton is not null && _webView is not null)
            _forwardButton.IsEnabled = _webView.CanGoForward;
    }

    private void HandleNavigationFailure(Uri failedUri, string reason)
    {
        if (!ShowDevelopmentToolbar)
            return;

        _isErrorPageActive = true;
        if (_statusText is not null)
            _statusText.Text = "Failed";
        if (_webView is null)
            return;

        var html = PluginWebViewDevTools.CreateDevelopmentErrorHtml(
            PluginId ?? "unknown",
            failedUri.ToString(),
            reason);

        try
        {
            _webView.NavigateToString(html, ErrorPageBaseUri);
            if (_routeText is not null)
                _routeText.Text = PluginWebViewDevTools.GetRouteText(failedUri, _routeBasePath);
            if (_statusText is not null)
                _statusText.Text = "Failed - retry available";
        }
        catch
        {
            if (_statusText is not null)
                _statusText.Text = "Failed";
        }
    }

    private void RetryTarget()
    {
        if (_webView is null || _targetUri is null)
            return;

        _isErrorPageActive = false;
        _webView.Source = _targetUri;
        if (_statusText is not null)
            _statusText.Text = "Loading";
    }

    private void OnDevBackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_webView is not null && _webView.CanGoBack)
            _webView.GoBack();
    }

    private void OnDevForwardClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_webView is not null && _webView.CanGoForward)
            _webView.GoForward();
    }

    private void OnDevRefreshClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isErrorPageActive)
        {
            RetryTarget();
            return;
        }

        _webView?.Refresh();
    }
}
