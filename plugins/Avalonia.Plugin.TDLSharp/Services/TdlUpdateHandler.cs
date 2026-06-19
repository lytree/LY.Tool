using Microsoft.Extensions.Logging;
using TdLib;

namespace Avalonia.Plugin.TDLSharp.Services;

public class TdlUpdateHandler
{
    private readonly ManualResetEventSlim _readyToAuthenticate;
    private readonly ILogger _logger;

    private Action<TdClient, string, ILogger>? _onAuthWaitPhoneNumber;
    private Action? _onAuthWaitCode;
    private Action? _onAuthWaitPassword;
    private Action? _onAuthWaitRegistration;
    private Action? _onAuthWaitOtherDeviceConfirmation;
    private Action? _onAuthWaitEmailAddress;
    private Action? _onAuthWaitEmailCode;
    private Action? _onAuthReady;
    private Action? _onAuthStateChanged;
    private Func<TdClient, string, ILogger, Task>? _onConfigureTdlibParameters;
    private Func<TdApi.File, string, ILogger, Task>? _onFileUpdate;
    private Func<TdApi.Update, ILogger, Task>? _onMessageUpdate;

    public bool AuthNeeded { get; private set; }
    public bool PasswordNeeded { get; private set; }
    public bool IsAuthenticated { get; private set; }
    public string AuthState { get; private set; } = "Unknown";
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
    public TdlUpdateHandler OnAuthWaitEmailAddress(Action handler) { _onAuthWaitEmailAddress = handler; return this; }
    public TdlUpdateHandler OnAuthWaitEmailCode(Action handler) { _onAuthWaitEmailCode = handler; return this; }
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
            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitTdlibParameters }:
                AuthState = "WaitTdlibParameters";
                _onAuthStateChanged?.Invoke();
                if (_onConfigureTdlibParameters != null)
                    await _onConfigureTdlibParameters(client, outputPath, logger);
                break;
            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitPhoneNumber }:
                AuthNeeded = true;
                PasswordNeeded = false;
                IsAuthenticated = false;
                AuthState = "WaitPhoneNumber";
                _readyToAuthenticate.Set();
                _onAuthStateChanged?.Invoke();
                _onAuthWaitPhoneNumber?.Invoke(client, outputPath, logger);
                break;
            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitCode }:
                AuthNeeded = true;
                AuthState = "WaitCode";
                _readyToAuthenticate.Set();
                _onAuthStateChanged?.Invoke();
                _onAuthWaitCode?.Invoke();
                break;
            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitPassword }:
                AuthNeeded = true;
                PasswordNeeded = true;
                AuthState = "WaitPassword";
                _readyToAuthenticate.Set();
                _onAuthStateChanged?.Invoke();
                _onAuthWaitPassword?.Invoke();
                break;
            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitRegistration }:
                AuthNeeded = true;
                AuthState = "WaitRegistration";
                _readyToAuthenticate.Set();
                _onAuthStateChanged?.Invoke();
                _onAuthWaitRegistration?.Invoke();
                break;
            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitOtherDeviceConfirmation state }:
                AuthState = "WaitOtherDeviceConfirmation";
                QrCodeLink = state.Link;
                _onAuthStateChanged?.Invoke();
                _onAuthWaitOtherDeviceConfirmation?.Invoke();
                break;
            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitEmailAddress }:
                AuthNeeded = true;
                AuthState = "WaitEmailAddress";
                _readyToAuthenticate.Set();
                _onAuthStateChanged?.Invoke();
                _onAuthWaitEmailAddress?.Invoke();
                break;
            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitEmailCode }:
                AuthNeeded = true;
                AuthState = "WaitEmailCode";
                _readyToAuthenticate.Set();
                _onAuthStateChanged?.Invoke();
                _onAuthWaitEmailCode?.Invoke();
                break;
            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitPremiumPurchase }:
                AuthState = "WaitPremiumPurchase";
                _onAuthStateChanged?.Invoke();
                logger.LogWarning("需要购买 Premium 才能继续操作");
                break;
            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateReady }:
                AuthNeeded = false;
                PasswordNeeded = false;
                IsAuthenticated = true;
                AuthState = "Ready";
                _readyToAuthenticate.Set();
                _onAuthStateChanged?.Invoke();
                _onAuthReady?.Invoke();
                break;
            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateLoggingOut }:
                AuthState = "LoggingOut";
                IsAuthenticated = false;
                _onAuthStateChanged?.Invoke();
                logger.LogDebug("正在登出...");
                break;
            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateClosing }:
                AuthState = "Closing";
                _onAuthStateChanged?.Invoke();
                logger.LogDebug("TDLib 正在关闭...");
                break;
            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateClosed }:
                AuthState = "Closed";
                IsAuthenticated = false;
                _onAuthStateChanged?.Invoke();
                logger.LogDebug("TDLib 已关闭");
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
}
