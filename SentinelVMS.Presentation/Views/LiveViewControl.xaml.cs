using System.Windows;
using System.Windows.Controls;
using SentinelVMS.Presentation.ViewModels;

namespace SentinelVMS.Presentation.Views;

public partial class LiveViewControl : UserControl
{
    public LiveViewControl()
    {
        InitializeComponent();
    }

    private async void ExitSingleTileMode_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is LiveViewViewModel vm)
        {
            await vm.ToggleSingleTileMode(null);
        }
    }
}
