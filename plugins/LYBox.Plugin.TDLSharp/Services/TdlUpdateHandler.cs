using Microsoft.Extensions.Logging;
using TdLib;

namespace LYBox.Plugin.TDLSharp.Services;

public class TdlUpdateHandler
{
    private readonly ManualResetEventSlim _readyToAuthenticate;
    private readonly ILogger _logger;

    private Action<TdClient, string, ILogger>? _onAuthWaitPhoneNumber;
    private Action? _onAuthWaitCode;
    private Action? _onAuthWaitPassword;
    private Action? _onAuthWaitRegistration;
    private Action? _onAuthWaitOtherDeviceConfirmation;
    private Action? _onAuthReady;
    private Action? _onAuthStateChanged;
    private Func<TdClient, string, ILogger, Task>? _onConfigureTdlibParameters;
    private Func<TdApi.File, string, ILogger, Task>? _onFileUpdate;
    private Func<TdApi.Update, ILogger, Task>? _onMessageUpdate;

    public bool AuthNeeded { get; private set; }
    public bool PasswordNeeded { get; private set; }
    public bool IsAuthenticated { get; private set; }
    public AuthStateCode AuthState { get; private set; } = AuthStateCode.Unknown;
    public string? QrCodeLink { get; private set; }

    public TdlUpdateHandler(ManualResetEventSlim readyToAuthenticate, ILogger logger)
    {
        _readyToAuthenticate = readyToAuthenticate;
        _logger = logger;
    }

    public TdlUpdateHandler OnAuthWaitPhoneNumber(Action<TdClient, string, ILogger> handler) { _onAuthWaitPhoneNumber = handler; return this; }
    public TdlUpdateHandler OnAuthWaitCode(Action handler) { _onAuthWaitCode = handler; return this; }
    public TdlUpdateHandler OnAuthWaitPassword(Action handler) { _onAuthWaitPassword = handler; return this; }
    public TdlUpdateHandler OnAuthWaitRegistration(Action handler) { _onAuthWaitRegistration = handler; return this; }
    public TdlUpdateHandler OnAuthWaitOtherDeviceConfirmation(Action handler) { _onAuthWaitOtherDeviceConfirmation = handler; return this; }
    public TdlUpdateHandler OnAuthReady(Action handler) { _onAuthReady = handler; return this; }
    public TdlUpdateHandler OnAuthStateChanged(Action handler) { _onAuthStateChanged = handler; return this; }
    public TdlUpdateHandler OnConfigureTdlibParameters(Func<TdClient, string, ILogger, Task> handler) { _onConfigureTdlibParameters = handler; return this; }
    public TdlUpdateHandler OnFileUpdate(Func<TdApi.File, string, ILogger, Task> handler) { _onFileUpdate = handler; return this; }
    public TdlUpdateHandler OnMessageUpdate(Func<TdApi.Update, ILogger, Task> handler) { _onMessageUpdate = handler; return this; }

    public async Task ProcessUpdates(TdClient client, TdApi.Update update, string outputPath)
    {
        var logger = _logger;

        switch (update)
        {
            #region UpdateAuthorizationState
            case TdApi.Update.UpdateAuthorizationState uas:
                HandleAuthorizationState(client, uas.AuthorizationState, outputPath, logger);
                break;
            #endregion

            #region UpdateConnectionState
            case TdApi.Update.UpdateConnectionState { State: TdApi.ConnectionState.ConnectionStateWaitingForNetwork }:
                logger.LogWarning("等待网络连接...");
                break;
            case TdApi.Update.UpdateConnectionState { State: TdApi.ConnectionState.ConnectionStateConnecting }:
                logger.LogDebug("正在连接到 Telegram 服务器...");
                break;
            case TdApi.Update.UpdateConnectionState { State: TdApi.ConnectionState.ConnectionStateConnectingToProxy }:
                logger.LogDebug("正在通过代理连接...");
                break;
            case TdApi.Update.UpdateConnectionState { State: TdApi.ConnectionState.ConnectionStateReady }:
                logger.LogDebug("已连接到 Telegram 服务器");
                break;
            case TdApi.Update.UpdateConnectionState { State: TdApi.ConnectionState.ConnectionStateUpdating }:
                logger.LogDebug("正在更新数据...");
                break;
            #endregion

            #region UpdateFile
            case TdApi.Update.UpdateFile updateFile:
                if (_onFileUpdate != null)
                    await _onFileUpdate(updateFile.File, outputPath, logger);
                break;
            case TdApi.Update.UpdateFileGenerationStart ufgStart:
                logger.LogDebug("文件生成开始: {Id}", ufgStart.GenerationId);
                break;
            case TdApi.Update.UpdateFileGenerationStop ufgStop:
                logger.LogDebug("文件生成结束: {Id}", ufgStop.GenerationId);
                break;
            #endregion

            #region UpdateUser
            case TdApi.Update.UpdateUser:
                _readyToAuthenticate.Set();
                break;
            #endregion

            #region UpdateNewMessage / UpdateMessage
            case TdApi.Update.UpdateNewMessage unm:
                logger.LogTrace("新消息: ChatId={ChatId}, MsgId={MsgId}", unm.Message.ChatId, unm.Message.Id);
                if (_onMessageUpdate != null) await _onMessageUpdate(unm, logger);
                break;
            case TdApi.Update.UpdateMessageSendSucceeded umss:
                logger.LogTrace("消息发送成功: MsgId={MsgId}", umss.Message.Id);
                if (_onMessageUpdate != null) await _onMessageUpdate(umss, logger);
                break;
            case TdApi.Update.UpdateMessageSendFailed umsf:
                logger.LogWarning("消息发送失败: MsgId={MsgId}, 错误: {Error}", umsf.Message.Id, umsf.Error.Message);
                if (_onMessageUpdate != null) await _onMessageUpdate(umsf, logger);
                break;
            case TdApi.Update.UpdateDeleteMessages udm:
                logger.LogTrace("消息删除: ChatId={ChatId}, 数量={Count}", udm.ChatId, udm.MessageIds.Length);
                if (_onMessageUpdate != null) await _onMessageUpdate(udm, logger);
                break;
            #endregion

            #region UpdateChat
            case TdApi.Update.UpdateNewChat unc:
                logger.LogTrace("新聊天: ChatId={ChatId}, Title={Title}", unc.Chat.Id, unc.Chat.Title);
                break;
            case TdApi.Update.UpdateChatTitle uct:
                logger.LogTrace("聊天标题更新: ChatId={ChatId}, Title={Title}", uct.ChatId, uct.Title);
                break;
            #endregion

            #region UpdateOption
            case TdApi.Update.UpdateOption uo:
                logger.LogTrace("选项更新: {Name} = {Value}", uo.Name, uo.Value);
                break;
            #endregion

            default:
                break;
        }
    }

    void HandleAuthorizationState(TdClient client, TdApi.AuthorizationState state, string outputPath, ILogger logger)
    {
        AuthState = state.ToAuthStateCode();

        switch (AuthState)
        {
            case AuthStateCode.WaitTdlibParameters:
                _onAuthStateChanged?.Invoke();
                if (_onConfigureTdlibParameters != null)
                    _ = _onConfigureTdlibParameters(client, outputPath, logger);
                break;
            case AuthStateCode.WaitPhoneNumber:
                AuthNeeded = true;
                PasswordNeeded = false;
                IsAuthenticated = false;
                _readyToAuthenticate.Set();
                _onAuthStateChanged?.Invoke();
                _onAuthWaitPhoneNumber?.Invoke(client, outputPath, logger);
                break;
            case AuthStateCode.WaitCode:
                AuthNeeded = true;
                _readyToAuthenticate.Set();
                _onAuthStateChanged?.Invoke();
                _onAuthWaitCode?.Invoke();
                break;
            case AuthStateCode.WaitPassword:
                AuthNeeded = true;
                PasswordNeeded = true;
                _readyToAuthenticate.Set();
                _onAuthStateChanged?.Invoke();
                _onAuthWaitPassword?.Invoke();
                break;
            case AuthStateCode.WaitRegistration:
                AuthNeeded = true;
                _readyToAuthenticate.Set();
                _onAuthStateChanged?.Invoke();
                _onAuthWaitRegistration?.Invoke();
                break;
            case AuthStateCode.WaitOtherDeviceConfirmation:
                AuthNeeded = true;
                _readyToAuthenticate.Set();
                _onAuthStateChanged?.Invoke();
                _onAuthWaitOtherDeviceConfirmation?.Invoke();
                break;
            case AuthStateCode.WaitEmailAddress:
            case AuthStateCode.WaitEmailCode:
            case AuthStateCode.WaitPremiumPurchase:
                AuthNeeded = true;
                _readyToAuthenticate.Set();
                _onAuthStateChanged?.Invoke();
                break;
            case AuthStateCode.Ready:
                IsAuthenticated = true;
                _readyToAuthenticate.Set();
                _onAuthStateChanged?.Invoke();
                _onAuthReady?.Invoke();
                break;
            case AuthStateCode.LoggingOut:
                IsAuthenticated = false;
                _onAuthStateChanged?.Invoke();
                logger.LogDebug("正在登出...");
                break;
            case AuthStateCode.Closing:
                _onAuthStateChanged?.Invoke();
                logger.LogDebug("TDLib 正在关闭...");
                break;
            case AuthStateCode.Closed:
                IsAuthenticated = false;
                _onAuthStateChanged?.Invoke();
                logger.LogDebug("TDLib 已关闭");
                break;
            case AuthStateCode.Unknown:
            default:
                _onAuthStateChanged?.Invoke();
                break;
        }
    }
}
