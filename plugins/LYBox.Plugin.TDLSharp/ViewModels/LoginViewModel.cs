using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.TDLSharp.Resources;
using LYBox.Plugin.TDLSharp.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using TdLib;
using Ursa.Controls;

namespace LYBox.Plugin.TDLSharp.ViewModels;

public enum LoginMethod
{
    PhoneNumber,
    BotToken,
    QrCode
}

public enum AuthStep
{
    Idle,
    WaitPhoneNumber,
    WaitCode,
    WaitPassword,
    WaitRegistration,
    WaitOtherDeviceConfirmation,
    Ready,
    Error
}

[ViewMap(typeof(Controls.LoginDialog))]
public partial class LoginViewModel : ViewModelBase, IDialogContext
{
    private readonly TdlClientManager _clientManager;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPhoneLogin), nameof(IsBotLogin), nameof(IsQrCodeLogin),
                              nameof(CanSubmitPhone), nameof(CanSubmitBotToken), nameof(CanRequestQrCode))]
    private LoginMethod _selectedLoginMethod = LoginMethod.PhoneNumber;

    [ObservableProperty] private string _phoneNumber = string.Empty;
    [ObservableProperty] private string _authCode = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _botToken = string.Empty;
    [ObservableProperty] private string? _qrCodeLink;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmitPhone), nameof(CanSubmitCode), nameof(CanSubmitPassword),
                              nameof(CanSubmitBotToken), nameof(CanRequestQrCode),
                              nameof(IsAuthenticated), nameof(NeedsInitialization))]
    private AuthStep _currentStep = AuthStep.Idle;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _userInfo = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _tdlRootPath = string.Empty;

    public bool HasTdlRoot => _clientManager.HasTdlRoot;
    public bool NeedsTdlRoot => !HasTdlRoot;
    public bool IsTdlInitialized => _clientManager.IsTdlInitialized;
    public bool NeedsInitialization => HasTdlRoot && !IsTdlInitialized && !IsAuthenticated;
    public bool IsPhoneLogin => SelectedLoginMethod == LoginMethod.PhoneNumber;
    public bool IsBotLogin => SelectedLoginMethod == LoginMethod.BotToken;
    public bool IsQrCodeLogin => SelectedLoginMethod == LoginMethod.QrCode;
    public bool CanSubmitPhone => CurrentStep is AuthStep.Idle or AuthStep.WaitPhoneNumber;
    public bool CanSubmitCode => CurrentStep == AuthStep.WaitCode;
    public bool CanSubmitPassword => CurrentStep == AuthStep.WaitPassword;
    public bool CanSubmitBotToken => CurrentStep is AuthStep.Idle or AuthStep.WaitPhoneNumber;
    public bool CanRequestQrCode => CurrentStep is AuthStep.Idle or AuthStep.WaitPhoneNumber;
    public bool IsAuthenticated => CurrentStep == AuthStep.Ready;

    public void Close()
    {
        RequestClose?.Invoke(this, null);
    }

    public event EventHandler<object?>? RequestClose;

    public LoginViewModel()
    {
        _clientManager = ServiceLocator.GetService<TdlClientManager>()!;
        _clientManager.AuthStateChanged += OnAuthStateChanged;
        TdlRootPath = _clientManager.TdlRoot;
        UpdateStepFromClient();
    }

    private void OnAuthStateChanged()
    {
        Dispatcher.UIThread.Post(UpdateStepFromClient);
    }

    private void UpdateStepFromClient()
    {
        CurrentStep = _clientManager.AuthState switch
        {
            AuthStateCode.Ready => AuthStep.Ready,
            AuthStateCode.WaitPhoneNumber => AuthStep.WaitPhoneNumber,
            AuthStateCode.WaitCode => AuthStep.WaitCode,
            AuthStateCode.WaitPassword => AuthStep.WaitPassword,
            AuthStateCode.WaitRegistration => AuthStep.WaitRegistration,
            AuthStateCode.WaitOtherDeviceConfirmation => AuthStep.WaitOtherDeviceConfirmation,
            _ => AuthStep.Idle,
        };

        QrCodeLink = _clientManager.QrCodeLink;

        StatusMessage = CurrentStep switch
        {
            AuthStep.Idle => Strings.Get("LOGIN_StatusIdle"),
            AuthStep.WaitPhoneNumber => Strings.Get("LOGIN_StatusWaitPhone"),
            AuthStep.WaitCode => Strings.Get("LOGIN_StatusWaitCode"),
            AuthStep.WaitPassword => Strings.Get("LOGIN_StatusWaitPassword"),
            AuthStep.WaitRegistration => Strings.Get("LOGIN_StatusWaitRegistration"),
            AuthStep.WaitOtherDeviceConfirmation => Strings.Get("LOGIN_StatusOtherDevice"),
            AuthStep.Ready => Strings.Get("LOGIN_StatusReady"),
            _ => string.Empty,
        };
    }

    private async Task ExecuteWithBusyAsync(Func<Task> action, Func<Exception, string> errorMessage)
    {
        if (IsBusy) return;
        IsBusy = true;
        try { await action(); }
        catch (TdException ex) { StatusMessage = errorMessage(ex); }
        catch (Exception ex) { StatusMessage = errorMessage(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task Initialize() => await ExecuteWithBusyAsync(async () =>
    {
        if (!HasTdlRoot)
        {
            StatusMessage = Strings.Get("LOGIN_TdlRootNotSet");
            return;
        }
        await _clientManager.EnsureInitializedAsync();
        StatusMessage = Strings.Get("LOGIN_Initialized");
    }, ex => Strings.Get("LOGIN_InitFailed", ex.Message));

    [RelayCommand]
    private async Task SubmitPhone() => await ExecuteWithBusyAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber)) return;
        await _clientManager.EnsureInitializedAsync();
        await _clientManager.AuthenticateAsync(PhoneNumber);
        StatusMessage = Strings.Get("LOGIN_PhoneSubmitted");
    }, ex => Strings.Get("LOGIN_PhoneFailed", ex.Message));

    [RelayCommand]
    private async Task SubmitCode() => await ExecuteWithBusyAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(AuthCode)) return;
        await _clientManager.SubmitAuthCode(AuthCode);
        AuthCode = string.Empty;
        StatusMessage = Strings.Get("LOGIN_CodeSubmitted");
    }, ex => Strings.Get("LOGIN_CodeFailed", ex.Message));

    [RelayCommand]
    private async Task SubmitPassword() => await ExecuteWithBusyAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(Password)) return;
        await _clientManager.SubmitPassword(Password);
        Password = string.Empty;
        StatusMessage = Strings.Get("LOGIN_PasswordSubmitted");
    }, ex => Strings.Get("LOGIN_PasswordFailed", ex.Message));

    [RelayCommand]
    private async Task SubmitBotToken() => await ExecuteWithBusyAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(BotToken)) return;
        await _clientManager.EnsureInitializedAsync();
        await _clientManager.AuthenticateWithBotTokenAsync(BotToken);
        StatusMessage = Strings.Get("LOGIN_BotTokenSubmitted");
    }, ex => Strings.Get("LOGIN_BotTokenFailed", ex.Message));

    [RelayCommand]
    private async Task RequestQrCode() => await ExecuteWithBusyAsync(async () =>
    {
        await _clientManager.EnsureInitializedAsync();
        await _clientManager.RequestQrCodeAuthenticationAsync();
        StatusMessage = Strings.Get("LOGIN_QrCodeRequested");
    }, ex => Strings.Get("LOGIN_QrCodeFailed", ex.Message));

    [RelayCommand]
    private async Task Logout() => await ExecuteWithBusyAsync(async () =>
    {
        await _clientManager.LogoutAsync();
        UserInfo = string.Empty;
        StatusMessage = Strings.Get("LOGIN_LoggedOut");
    }, ex => Strings.Get("LOGIN_LogoutFailed", ex.Message));

    public override void Dispose()
    {
        _clientManager.AuthStateChanged -= OnAuthStateChanged;
        base.Dispose();
    }
}
