using System.Windows;
using SentinelVMS.Presentation.ViewModels;

namespace SentinelVMS.Presentation.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        PasswordBox.Password = _viewModel.Password;
        _viewModel.RequestClose += OnRequestClose;
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = PasswordBox.Password;
    }

    private void OnRequestClose(bool result)
    {
        DialogResult = result;
        Close();
    }
}
