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

    private void ExitSingleTileMode_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is LiveViewViewModel vm)
        {
            vm.IsSingleTileMode = false;
            vm.FocusedTile = null;
        }
    }
}
