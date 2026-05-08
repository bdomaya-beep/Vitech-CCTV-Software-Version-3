using SentinelVMS.Streaming.Models;

namespace SentinelVMS.Streaming.Core;

public sealed class FfmpegDecoder : IDecoder
{
    public ValueTask<VideoFrame> DecodeAsync(MediaPacket packet, CancellationToken cancellationToken = default)
    {
        // Real FFmpeg decode wiring belongs here; this returns a managed frame envelope for pipeline continuity.
        var fakeFrame = new VideoFrame
        {
            ChannelId = packet.ChannelId,
            Width = 640,
            Height = 360,
            Stride = 640 * 4,
            PixelData = new byte[640 * 360 * 4]
        };

        return ValueTask.FromResult(fakeFrame);
    }
}
