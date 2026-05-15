using System.Windows;
using System.Windows.Input;
using SentinelVMS.Presentation.ViewModels;

namespace SentinelVMS.Presentation.Views;

public partial class DetachedLiveViewWindow : Window
{
    private readonly DetachedLiveViewViewModel _viewModel;

    public DetachedLiveViewWindow(DetachedLiveViewViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Closed += OnClosed;
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        try
        {
            await _viewModel.LiveViewModel.StopAllConnectedTilesAsync();
        }
        catch
        {
            // best-effort cleanup only
        }

        _viewModel.Dispose();
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
