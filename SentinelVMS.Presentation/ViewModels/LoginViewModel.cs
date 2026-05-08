using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SentinelVMS.Application.Abstractions.Authentication;
using SentinelVMS.Application.DTOs;
using SentinelVMS.Presentation.Core;

namespace SentinelVMS.Presentation.ViewModels;

public partial class LoginViewModel(IAuthenticationService authenticationService) : ViewModelBase
{
    public event Action<bool>? RequestClose;

    [ObservableProperty]
    private string _username = "admin";

    [ObservableProperty]
    private string _password = "Admin@123";

    [ObservableProperty]
    private bool _rememberMe = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        var result = await authenticationService.LoginAsync(new LoginRequest(Username.Trim(), Password, RememberMe));
        IsBusy = false;

        if (!result.Succeeded)
        {
            ErrorMessage = result.Message;
            return;
        }

        RequestClose?.Invoke(true);
    }
}
