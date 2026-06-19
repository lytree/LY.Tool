using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Resources;
using Avalonia.Plugin.TDLSharp.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using TdLib;
using Ursa.Controls;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

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

    [ObservableProperty] private LoginMethod _selectedLoginMethod = LoginMethod.PhoneNumber;
    [ObservableProperty] private string _phoneNumber = string.Empty;
    [ObservableProperty] private string _authCode = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _botToken = string.Empty;
    [ObservableProperty] private string? _qrCodeLink;
    [ObservableProperty] private AuthStep _currentStep = AuthStep.Idle;
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

    partial void OnSelectedLoginMethodChanged(LoginMethod value)
    {
        OnPropertyChanged(nameof(IsPhoneLogin));
        OnPropertyChanged(nameof(IsBotLogin));
        OnPropertyChanged(nameof(IsQrCodeLogin));
        OnPropertyChanged(nameof(CanSubmitPhone));
        OnPropertyChanged(nameof(CanSubmitBotToken));
        OnPropertyChanged(nameof(CanRequestQrCode));
    }

    partial void OnCurrentStepChanged(AuthStep value)
    {
        OnPropertyChanged(nameof(CanSubmitPhone));
        OnPropertyChanged(nameof(CanSubmitCode));
        OnPropertyChanged(nameof(CanSubmitPassword));
        OnPropertyChanged(nameof(CanSubmitBotToken));
        OnPropertyChanged(nameof(CanRequestQrCode));
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(NeedsInitialization));
    }

    private void OnAuthStateChanged()
    {
        Dispatcher.UIThread.Post(UpdateStepFromClient);
    }

    private void UpdateStepFromClient()
    {
        var state = _clientManager.AuthState;
        CurrentStep = state switch
        {
            "Ready" => AuthStep.Ready,
            "WaitPhoneNumber" => AuthStep.WaitPhoneNumber,
            "WaitCode" => AuthStep.WaitCode,
            "WaitPassword" => AuthStep.WaitPassword,
            "WaitRegistration" => AuthStep.WaitRegistration,
            "WaitOtherDeviceConfirmation" => AuthStep.WaitOtherDeviceConfirmation,
            _ => AuthStep.Idle
        };

        QrCodeLink = _clientManager.QrCodeLink;

        StatusMessage = CurrentStep switch
        {
            AuthStep.Idle => Strings.Get("LOGIN_StatusIdle"),
            AuthStep.WaitPhoneNumber => Strings.Get("LOGIN_StatusWaitPhone"),
            AuthStep.WaitCode => Strings.Get("LOGIN_StatusWaitCode"),
            AuthStep.WaitPassword => Strings.Get("LOGIN_StatusWaitPassword"),
            AuthStep.WaitRegistration => Strings.Get("LOGIN_StatusWaitRegistration"),
            AuthStep.WaitOtherDeviceConfirmation => Strings.Get("LOGIN_StatusWaitOtherDevice"),
            AuthStep.Ready => Strings.Get("LOGIN_StatusReady"),
            _ => Strings.Get("LOGIN_StatusError")
        };

        if (CurrentStep == AuthStep.Ready)
        {
            _ = LoadUserInfoAsync();
            // Auto-close dialog after successful authentication
            _ = AutoCloseDialogAsync();
        }
    }

    private async Task LoadUserInfoAsync()
    {
        try
        {
            var user = await _clientManager.GetCurrentUserAsync();
            var name = string.IsNullOrWhiteSpace(user.LastName)
                ? user.FirstName
                : $"{user.FirstName} {user.LastName}";
            var username = user.Usernames?.ActiveUsernames?.FirstOrDefault();
            UserInfo = string.IsNullOrEmpty(username)
                ? $"{name} (ID: {user.Id})"
                : $"{name} (@{username})";
        }
        catch
        {
            UserInfo = Strings.Get("LOGIN_UserInfoUnavailable");
        }
    }

    private async Task AutoCloseDialogAsync()
    {
        await Task.Delay(500);
        Dispatcher.UIThread.Post(() => RequestClose?.Invoke(this, true));
    }

    [RelayCommand]
    private async Task Initialize()
    {
        if (IsBusy) return;
        if (!HasTdlRoot)
        {
            StatusMessage = Strings.Get("LOGIN_TdlRootNotSet");
            return;
        }
        IsBusy = true;
        try
        {
            await _clientManager.EnsureInitializedAsync();
            StatusMessage = Strings.Get("LOGIN_Initialized");
        }
        catch (Exception ex)
        {
            StatusMessage = Strings.Get("LOGIN_InitFailed", ex.Message);
            CurrentStep = AuthStep.Error;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SubmitPhone()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(PhoneNumber)) return;
        IsBusy = true;
        try
        {
            await _clientManager.EnsureInitializedAsync();
            await _clientManager.AuthenticateAsync(PhoneNumber);
            StatusMessage = Strings.Get("LOGIN_PhoneSubmitted");
        }
        catch (TdException ex)
        {
            StatusMessage = Strings.Get("LOGIN_PhoneFailed", ex.Error.Message);
        }
        catch (Exception ex)
        {
            StatusMessage = Strings.Get("LOGIN_PhoneFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SubmitCode()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(AuthCode)) return;
        IsBusy = true;
        try
        {
            await _clientManager.SubmitAuthCode(AuthCode);
            AuthCode = string.Empty;
            StatusMessage = Strings.Get("LOGIN_CodeSubmitted");
        }
        catch (TdException ex)
        {
            StatusMessage = Strings.Get("LOGIN_CodeFailed", ex.Error.Message);
        }
        catch (Exception ex)
        {
            StatusMessage = Strings.Get("LOGIN_CodeFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SubmitPassword()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(Password)) return;
        IsBusy = true;
        try
        {
            await _clientManager.SubmitPassword(Password);
            Password = string.Empty;
            StatusMessage = Strings.Get("LOGIN_PasswordSubmitted");
        }
        catch (TdException ex)
        {
            StatusMessage = Strings.Get("LOGIN_PasswordFailed", ex.Error.Message);
        }
        catch (Exception ex)
        {
            StatusMessage = Strings.Get("LOGIN_PasswordFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SubmitBotToken()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(BotToken)) return;
        IsBusy = true;
        try
        {
            await _clientManager.EnsureInitializedAsync();
            await _clientManager.AuthenticateWithBotTokenAsync(BotToken);
            StatusMessage = Strings.Get("LOGIN_BotTokenSubmitted");
        }
        catch (TdException ex)
        {
            StatusMessage = Strings.Get("LOGIN_BotTokenFailed", ex.Error.Message);
        }
        catch (Exception ex)
        {
            StatusMessage = Strings.Get("LOGIN_BotTokenFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RequestQrCode()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await _clientManager.EnsureInitializedAsync();
            await _clientManager.RequestQrCodeAuthenticationAsync();
            StatusMessage = Strings.Get("LOGIN_QrCodeRequested");
        }
        catch (TdException ex)
        {
            StatusMessage = Strings.Get("LOGIN_QrCodeFailed", ex.Error.Message);
        }
        catch (Exception ex)
        {
            StatusMessage = Strings.Get("LOGIN_QrCodeFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Logout()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await _clientManager.LogoutAsync();
            UserInfo = string.Empty;
            StatusMessage = Strings.Get("LOGIN_LoggedOut");
        }
        catch (Exception ex)
        {
            StatusMessage = Strings.Get("LOGIN_LogoutFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public override void Dispose()
    {
        _clientManager.AuthStateChanged -= OnAuthStateChanged;
        base.Dispose();
    }
}
