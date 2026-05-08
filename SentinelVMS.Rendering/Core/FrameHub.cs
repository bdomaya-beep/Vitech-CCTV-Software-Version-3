using System.Collections.Concurrent;
using SentinelVMS.Streaming.Models;

namespace SentinelVMS.Rendering.Core;

public static class FrameHub
{
    private static readonly ConcurrentDictionary<Guid, VideoFrame> LatestFrames = new();

    public static void Publish(VideoFrame frame)
    {
        LatestFrames[frame.ChannelId] = frame;
    }

    public static bool TryGet(Guid channelId, out VideoFrame frame)
    {
        return LatestFrames.TryGetValue(channelId, out frame!);
    }
}
