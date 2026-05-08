using SentinelVMS.Domain.Enums;

namespace SentinelVMS.Domain.Models;

public sealed class EventRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public Guid? ChannelId { get; set; }
    public EventType EventType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset OccurredUtc { get; set; } = DateTimeOffset.UtcNow;
}
