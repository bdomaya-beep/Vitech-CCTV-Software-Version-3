using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SentinelVMS.Presentation.ViewModels;
using SentinelVMS.Presentation.Views;

namespace SentinelVMS.Presentation.Views;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;

    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += OnLoaded;
        _viewModel.RequestOpenLivePopout += OnRequestOpenLivePopout;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private void OnRequestOpenLivePopout()
    {
        var liveViewWindow = new Window
        {
            Title = "Sentinel VMS - Detached Live View",
            Width = 1200,
            Height = 800,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = Background,
            Content = new LiveViewControl
            {
                DataContext = _viewModel.LiveViewModel
            }
        };

        liveViewWindow.Show();
    }

    private void TreeItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (sender is not FrameworkElement element || element.DataContext is not DeviceTreeItemViewModel item)
        {
            return;
        }

        if (!item.IsChannel)
        {
            return;
        }

        DragDrop.DoDragDrop(element, item, DragDropEffects.Copy);
    }
}
