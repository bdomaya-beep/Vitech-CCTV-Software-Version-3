using SentinelVMS.Streaming.Pipeline;
using SentinelVMS.Streaming.Models;
using SentinelVMS.Rendering.Core;

namespace SentinelVMS.Rendering.Wpf;

public sealed class D3DImageFrameSink(IDirectXRenderer renderer) : IFrameSink
{
    public ValueTask OnFrameAsync(VideoFrame frame, CancellationToken cancellationToken = default)
    {
        renderer.UploadFrame(frame);
        FrameHub.Publish(frame);
        return ValueTask.CompletedTask;
    }
}
