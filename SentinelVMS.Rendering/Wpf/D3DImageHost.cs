using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SentinelVMS.Rendering.Core;

namespace SentinelVMS.Rendering.Wpf;

public sealed class D3DImageHost : Image
{
    public static readonly DependencyProperty ChannelIdProperty = DependencyProperty.Register(
        nameof(ChannelId),
        typeof(Guid),
        typeof(D3DImageHost),
        new PropertyMetadata(Guid.Empty));

    private WriteableBitmap? _bitmap;

    public D3DImageHost()
    {
        Stretch = Stretch.UniformToFill;
        SnapsToDevicePixels = true;
        CompositionTarget.Rendering += OnRendering;
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnRendering;
    }

    public Guid ChannelId
    {
        get => (Guid)GetValue(ChannelIdProperty);
        set => SetValue(ChannelIdProperty, value);
    }

    public void Refresh(IDirectXRenderer renderer)
    {
        // no-op retained for backward compatibility
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (ChannelId == Guid.Empty)
        {
            return;
        }

        if (!FrameHub.TryGet(ChannelId, out var frame))
        {
            return;
        }

        if (_bitmap is null || _bitmap.PixelWidth != frame.Width || _bitmap.PixelHeight != frame.Height)
        {
            _bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null);
            Source = _bitmap;
        }

        _bitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), frame.PixelData, frame.Stride, 0);
    }
}
