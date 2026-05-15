using SentinelVMS.Presentation.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SentinelVMS.Presentation.Views;

public partial class DeviceManagementControl : UserControl
{
    public DeviceManagementControl()
    {
        InitializeComponent();
        PasswordInput.PasswordChanged += OnPasswordChanged;
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is DeviceManagementViewModel vm)
            vm.NewDevicePassword = PasswordInput.Password;
    }
}
