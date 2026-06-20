using Microsoft.Extensions.Logging;
using TdLib;
using TdLib.Bindings;

namespace Avalonia.Plugin.TDLSharp.Services;

public class TdlClientManager : IDisposable
{
    private static readonly object _initLock = new();

    private TdClient? _client;
    private TdlUpdateHandler? _updateHandler;
    private readonly ManualResetEventSlim _ready = new();
    private readonly ILogger _logger;
    private bool _initialized;
    private bool _disposed;

    public string TdlRoot { get; }
    public bool HasTdlRoot => !string.IsNullOrWhiteSpace(TdlRoot) && Directory.Exists(TdlRoot);
    public bool IsTdlInitialized => HasTdlRoot && Directory.EnumerateFileSystemEntries(TdlRoot).Any();
    public string ApiId { get; }
    public string ApiHash { get; }
    public string ProxyServer { get; }
    public int ProxyPort { get; }
    public bool EnableProxy { get; }

    public bool AuthNeeded => _updateHandler?.AuthNeeded ?? false;
    public bool PasswordNeeded => _updateHandler?.PasswordNeeded ?? false;
    public bool IsReady => _ready.IsSet;
    public bool IsAuthenticated => _updateHandler?.IsAuthenticated ?? false;
    public string AuthState => _updateHandler?.AuthState ?? "Unknown";
    public string? QrCodeLink => _updateHandler?.QrCodeLink;

    /// <summary>
    /// 根据 TdlUpdateHandler 的认证状态判断是否需要弹出登录界面。
    /// 需要登录的状态：WaitPhoneNumber / WaitCode / WaitPassword / WaitRegistration /
    /// WaitOtherDeviceConfirmation / WaitEmailAddress / WaitEmailCode / WaitPremiumPurchase /
    /// Unknown（未初始化）/ Closed / LoggingOut / Closing。
    /// 不需要登录的状态：Ready（已认证）/ WaitTdlibParameters（初始化中过渡态）。
    /// </summary>
    public bool NeedsLogin => AuthState is
        "WaitPhoneNumber" or "WaitCode" or "WaitPassword" or
        "WaitRegistration" or "WaitOtherDeviceConfirmation" or
        "WaitEmailAddress" or "WaitEmailCode" or "WaitPremiumPurchase" or
        "Unknown" or "Closed" or "LoggingOut" or "Closing";
    public TdClient Client => _client ?? throw new InvalidOperationException("Client not initialized. Call EnsureInitializedAsync first.");

    public event Func<TdApi.File, Task>? FileUpdated;
    public event Action? AuthStateChanged;

    public DirectUiLogger? FileUpdateLogger { get; set; }

    public TdlClientManager(ILogger<TdlClientManager> logger, string apiId, string apiHash,
        string proxyServer = "127.0.0.1", int proxyPort = 7897, bool enableProxy = true, string? tdlRootPath = null)
    {
        _logger = logger;
        ApiId = apiId;
        ApiHash = apiHash;
        ProxyServer = proxyServer;
        ProxyPort = proxyPort;
        EnableProxy = enableProxy;

        TdlRoot = string.IsNullOrWhiteSpace(tdlRootPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".tdl")
            : tdlRootPath;
        if (!Directory.Exists(TdlRoot))
        {
            Directory.CreateDirectory(TdlRoot);
        }
    }

    public async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        lock (_initLock)
        {
            if (_initialized) return;
        }

        _client = new TdClient();
        _client.Bindings.SetLogVerbosityLevel(TdLogLevel.Fatal);

        _updateHandler = new TdlUpdateHandler(_ready, _logger)
            .OnConfigureTdlibParameters(ConfigureTdlibParameters)
            .OnFileUpdate(HandleFileUpdate)
            .OnAuthStateChanged(() => AuthStateChanged?.Invoke());

        _client.UpdateReceived += async (_, update) =>
        {
            await _updateHandler.ProcessUpdates(_client, update, TdlRoot);
        };

        lock (_initLock)
        {
            _initialized = true;
        }
    }

    public async Task WaitReadyAsync()
    {
        _ready.Wait();
    }

    /// <summary>
    /// 确保 TDLib 客户端已初始化并已报告首个认证状态。
    /// 如果 AuthState 为 Unknown（未初始化），会先初始化客户端，
    /// 然后等待 TDLib 回报首个认证状态（Ready 或 Wait* 系列）。
    /// </summary>
    public async Task EnsureReadyForAuthCheckAsync(TimeSpan? timeout = null)
    {
        if (AuthState != "Unknown") return;

        await EnsureInitializedAsync();

        // 等待 TDLib 回报首个认证状态（Ready / WaitPhoneNumber / WaitCode 等）
        // _ready 在 WaitPhoneNumber/WaitCode/WaitPassword/WaitRegistration/
        // WaitEmailAddress/WaitEmailCode/Ready 时被 Set
        var waitTimeout = timeout ?? TimeSpan.FromSeconds(15);
        try
        {
            _ready.Wait(waitTimeout);
        }
        catch (InvalidOperationException)
        {
            // 超时：保持 AuthState 为 Unknown，NeedsLogin 仍返回 true
        }
    }

    public async Task AuthenticateAsync(string phoneNumber)
    {
        if (_client == null) throw new InvalidOperationException("Client not initialized.");
        await _client.ExecuteAsync(new TdApi.SetAuthenticationPhoneNumber
        {
            PhoneNumber = phoneNumber
        });
    }

    public async Task SubmitAuthCode(string code)
    {
        if (_client == null) throw new InvalidOperationException("Client not initialized.");
        await _client.ExecuteAsync(new TdApi.CheckAuthenticationCode { Code = code });
    }

    public async Task SubmitPassword(string password)
    {
        if (_client == null) throw new InvalidOperationException("Client not initialized.");
        await _client.ExecuteAsync(new TdApi.CheckAuthenticationPassword { Password = password });
    }

    public async Task AuthenticateWithBotTokenAsync(string botToken)
    {
        if (_client == null) throw new InvalidOperationException("Client not initialized.");
        await _client.ExecuteAsync(new TdApi.CheckAuthenticationBotToken { Token = botToken });
    }

    public async Task RequestQrCodeAuthenticationAsync()
    {
        if (_client == null) throw new InvalidOperationException("Client not initialized.");
        await _client.ExecuteAsync(new TdApi.RequestQrCodeAuthentication());
    }

    public async Task LogoutAsync()
    {
        if (_client == null) return;
        try
        {
            await _client.ExecuteAsync(new TdApi.LogOut());
        }
        catch (Exception ex) { _logger.LogWarning(ex, "注销失败"); }
    }

    public async Task<TdApi.User> GetCurrentUserAsync()
    {
        if (_client == null) throw new InvalidOperationException("Client not initialized.");
        return await _client.ExecuteAsync(new TdApi.GetMe());
    }

    public string GetTdlRoot() => TdlRoot;

    private async Task ConfigureTdlibParameters(TdClient client, string outputPath, ILogger cbLogger)
    {
        await client.ExecuteAsync(new TdApi.SetTdlibParameters
        {
            ApiId = int.TryParse(ApiId, out var id) ? id : 0,
            ApiHash = ApiHash,
            DeviceModel = "PC",
            SystemLanguageCode = "en",
            ApplicationVersion = "1.0.0",
            DatabaseDirectory = Path.Combine(TdlRoot, "db"),
            FilesDirectory = Path.Combine(TdlRoot, "files"),
            UseFileDatabase = true,
            UseChatInfoDatabase = true,
            UseMessageDatabase = true,
        });

        if (EnableProxy)
        {
            cbLogger.LogInformation("正在尝试连接代理...");
            var proxy = await client.AddProxyAsync(
                new TdApi.Proxy
                {
                    Server = ProxyServer,
                    Port = ProxyPort,
                    Type = new TdApi.ProxyType.ProxyTypeSocks5()
                }, true);
            await client.EnableProxyAsync(proxy.Id);
            cbLogger.LogInformation("代理已启用。");
        }
    }

    private Task HandleFileUpdate(TdApi.File file, string outputPath, ILogger cbLogger)
    {
        if (file.Local.IsDownloadingCompleted)
        {
            FileUpdateLogger?.Log($"文件下载完成！本地路径: {file.Local.Path}");
            FileUpdated?.Invoke(file);
        }
        else
        {
            double percent = (double)file.Local.DownloadedSize / file.ExpectedSize * 100;
            FileUpdateLogger?.Log($"文件下载中, File {file.Id}  进度: {percent:F1}%");
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _client?.Dispose();
        _ready.Dispose();
    }
}
