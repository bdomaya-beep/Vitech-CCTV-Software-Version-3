using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using SentinelVMS.Presentation.ViewModels;
using SentinelVMS.Presentation.Views;

namespace SentinelVMS.Presentation.Views;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private readonly IServiceProvider _services;

    public MainWindow(ShellViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _services = services;
        DataContext = _viewModel;
        Loaded += OnLoaded;
        _viewModel.RequestOpenLivePopout += OnRequestOpenLivePopout;
        KeyDown += OnWindowKeyDown;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private async void OnRequestOpenLivePopout()
    {
        try
        {
            var popoutLiveViewModel = _services.GetRequiredService<LiveViewViewModel>();
            await popoutLiveViewModel.LoadCommand.ExecuteAsync(null);

            var detachedViewModel = new DetachedLiveViewViewModel(_viewModel, popoutLiveViewModel);
            var liveViewWindow = new DetachedLiveViewWindow(detachedViewModel)
            {
                Owner = this
            };

            liveViewWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Unable to open detached live view.\n\n{ex.Message}",
                "Detached Live View",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _viewModel.IsFullscreen)
        {
            _viewModel.IsFullscreen = false;
        }
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

        DragDrop.DoDragDrop(element, item, DragDropEffects.Copy);
    }
}
