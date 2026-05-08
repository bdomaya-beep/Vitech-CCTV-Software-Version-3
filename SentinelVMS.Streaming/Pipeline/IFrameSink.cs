using SentinelVMS.Streaming.Models;

namespace SentinelVMS.Streaming.Pipeline;

public interface IFrameSink
{
    ValueTask OnFrameAsync(VideoFrame frame, CancellationToken cancellationToken = default);
}
