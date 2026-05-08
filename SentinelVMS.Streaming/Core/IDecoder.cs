using SentinelVMS.Streaming.Models;

namespace SentinelVMS.Streaming.Core;

public interface IDecoder
{
    ValueTask<VideoFrame> DecodeAsync(MediaPacket packet, CancellationToken cancellationToken = default);
}
