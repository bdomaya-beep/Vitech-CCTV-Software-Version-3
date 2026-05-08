using SentinelVMS.Streaming.Models;

namespace SentinelVMS.Streaming.Core;

public interface IRtspClient
{
    IAsyncEnumerable<MediaPacket> ReceivePacketsAsync(Guid channelId, string rtspUrl, CancellationToken cancellationToken = default);
}
