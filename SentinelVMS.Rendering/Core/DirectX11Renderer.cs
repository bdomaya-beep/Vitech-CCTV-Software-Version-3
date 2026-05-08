using System.Collections.Concurrent;
using SentinelVMS.Rendering.Wpf;
using SentinelVMS.Streaming.Models;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct3D11.D3D11;

namespace SentinelVMS.Rendering.Core;

public sealed class DirectX11Renderer : IDirectXRenderer
{
    private readonly ConcurrentDictionary<Guid, VideoFrame> _latestFrames = new();

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;

    public void Initialize()
    {
        if (_device is not null)
        {
            return;
        }

        D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            [FeatureLevel.Level_11_0, FeatureLevel.Level_10_1],
            out _device,
            out _context).CheckError();
    }

    public void UploadFrame(VideoFrame frame)
    {
        _latestFrames[frame.ChannelId] = frame;
    }

    public bool TryGetLatestFrame(Guid channelId, out VideoFrame frame)
    {
        return _latestFrames.TryGetValue(channelId, out frame!);
    }
}
