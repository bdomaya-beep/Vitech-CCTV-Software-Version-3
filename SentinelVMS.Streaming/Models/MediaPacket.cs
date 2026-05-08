namespace SentinelVMS.Streaming.Models;

public sealed class MediaPacket
{
    public required Guid ChannelId { get; init; }
    public required byte[] Data { get; init; }
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}
