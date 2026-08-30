using TdLib;

namespace LYBox.Plugin.TDLSharp.Services;

/// <summary>
/// TDLib 授权状态枚举（与 <c>TdApi.AuthorizationState.*</c> 一一对应）。
/// 取代原先在 <see cref="TdlClientManager"/> 与 <see cref="ViewModels.LoginViewModel"/>
/// 之间用魔法字符串传递的状态名。
/// </summary>
public enum AuthStateCode
{
    Unknown,
    WaitTdlibParameters,
    WaitPhoneNumber,
    WaitCode,
    WaitPassword,
    WaitRegistration,
    WaitOtherDeviceConfirmation,
    WaitEmailAddress,
    WaitEmailCode,
    WaitPremiumPurchase,
    Ready,
    LoggingOut,
    Closing,
    Closed,
}

/// <summary>
/// 将 <see cref="TdApi.AuthorizationState"/> 派生类映射为强类型 <see cref="AuthStateCode"/>。
/// </summary>
public static class AuthStateCodeExtensions
{
    public static AuthStateCode ToAuthStateCode(this TdApi.AuthorizationState state) => state switch
    {
        TdApi.AuthorizationState.AuthorizationStateWaitTdlibParameters => AuthStateCode.WaitTdlibParameters,
        TdApi.AuthorizationState.AuthorizationStateWaitPhoneNumber => AuthStateCode.WaitPhoneNumber,
        TdApi.AuthorizationState.AuthorizationStateWaitCode => AuthStateCode.WaitCode,
        TdApi.AuthorizationState.AuthorizationStateWaitPassword => AuthStateCode.WaitPassword,
        TdApi.AuthorizationState.AuthorizationStateWaitRegistration => AuthStateCode.WaitRegistration,
        TdApi.AuthorizationState.AuthorizationStateWaitOtherDeviceConfirmation => AuthStateCode.WaitOtherDeviceConfirmation,
        TdApi.AuthorizationState.AuthorizationStateWaitEmailAddress => AuthStateCode.WaitEmailAddress,
        TdApi.AuthorizationState.AuthorizationStateWaitEmailCode => AuthStateCode.WaitEmailCode,
        TdApi.AuthorizationState.AuthorizationStateWaitPremiumPurchase => AuthStateCode.WaitPremiumPurchase,
        TdApi.AuthorizationState.AuthorizationStateReady => AuthStateCode.Ready,
        TdApi.AuthorizationState.AuthorizationStateLoggingOut => AuthStateCode.LoggingOut,
        TdApi.AuthorizationState.AuthorizationStateClosing => AuthStateCode.Closing,
        TdApi.AuthorizationState.AuthorizationStateClosed => AuthStateCode.Closed,
        _ => AuthStateCode.Unknown,
    };

    public static bool NeedsLogin(this AuthStateCode code) => code is
        AuthStateCode.WaitPhoneNumber
        or AuthStateCode.WaitCode
        or AuthStateCode.WaitPassword
        or AuthStateCode.WaitRegistration
        or AuthStateCode.WaitOtherDeviceConfirmation
        or AuthStateCode.WaitEmailAddress
        or AuthStateCode.WaitEmailCode
        or AuthStateCode.WaitPremiumPurchase
        or AuthStateCode.Unknown
        or AuthStateCode.Closed
        or AuthStateCode.LoggingOut
        or AuthStateCode.Closing;

    public static bool IsAuthenticated(this AuthStateCode code) => code == AuthStateCode.Ready;
}
