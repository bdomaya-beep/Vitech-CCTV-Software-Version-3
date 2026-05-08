namespace SentinelVMS.Streaming.Models;

public sealed class VideoFrame
{
    public required Guid ChannelId { get; init; }
    public required byte[] PixelData { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int Stride { get; init; }
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}
