using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SentinelVMS.Presentation.ViewModels;

namespace SentinelVMS.Presentation.Controls;

public partial class LiveTileControl : UserControl
{
    private Point? _panStartPoint;
    private Point _panOrigin;

    public LiveTileControl()
    {
        InitializeComponent();
    }

    private void TileBorder_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(DeviceTreeItemViewModel))
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void TileBorder_OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not LiveTileViewModel tile)
        {
            return;
        }

        if (!e.Data.GetDataPresent(typeof(DeviceTreeItemViewModel)))
        {
            return;
        }

        var item = (DeviceTreeItemViewModel)e.Data.GetData(typeof(DeviceTreeItemViewModel));
        if (!item.IsChannel)
        {
            return;
        }

        var liveVm = FindLiveViewViewModel();
        if (liveVm is null)
        {
            return;
        }

        await liveVm.AssignChannelToTileAsync(item.Id, item.Name, tile);
    }

    private void TileBorder_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2)
        {
            return;
        }

        if (DataContext is not LiveTileViewModel tile)
        {
            return;
        }

        var liveVm = FindLiveViewViewModel();
        liveVm?.ToggleSingleTileMode(tile);
    }

    private void VideoViewport_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is LiveTileViewModel { IsPlaceholder: true })
        {
            return;
        }

        var step = e.Delta > 0 ? 0.15 : -0.15;
        var nextScale = Math.Clamp(VideoScaleTransform.ScaleX + step, 1.0, 6.0);
        VideoScaleTransform.ScaleX = nextScale;
        VideoScaleTransform.ScaleY = nextScale;

        if (Math.Abs(nextScale - 1.0) < 0.001)
        {
            ResetZoom();
        }

        e.Handled = true;
    }

    private void VideoViewport_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (VideoScaleTransform.ScaleX <= 1.0)
        {
            return;
        }

        _panStartPoint = e.GetPosition(VideoViewport);
        _panOrigin = new Point(VideoTranslateTransform.X, VideoTranslateTransform.Y);
        VideoViewport.CaptureMouse();
        e.Handled = true;
    }

    private void VideoViewport_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_panStartPoint is null || e.RightButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(VideoViewport);
        var delta = current - _panStartPoint.Value;
        VideoTranslateTransform.X = _panOrigin.X + delta.X;
        VideoTranslateTransform.Y = _panOrigin.Y + delta.Y;
    }

    private void VideoViewport_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _panStartPoint = null;
        VideoViewport.ReleaseMouseCapture();
    }

    private void ResetZoomButton_OnClick(object sender, RoutedEventArgs e)
    {
        ResetZoom();
    }

    private void ResetZoom()
    {
        VideoScaleTransform.ScaleX = 1;
        VideoScaleTransform.ScaleY = 1;
        VideoTranslateTransform.X = 0;
        VideoTranslateTransform.Y = 0;
        _panStartPoint = null;
        VideoViewport.ReleaseMouseCapture();
    }

    private LiveViewViewModel? FindLiveViewViewModel()
    {
        var itemsControl = FindAncestor<ItemsControl>(this);
        return itemsControl?.DataContext as LiveViewViewModel;
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        var parent = current;
        while (parent is not null)
        {
            parent = VisualTreeHelper.GetParent(parent);
            if (parent is T typed)
            {
                return typed;
            }
        }

        return null;
    }
}
