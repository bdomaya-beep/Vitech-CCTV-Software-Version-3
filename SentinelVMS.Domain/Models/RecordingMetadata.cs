namespace SentinelVMS.Domain.Models;

public sealed class RecordingMetadata
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChannelId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public long SizeBytes { get; set; }
}
