namespace SentinelVMS.Domain.Models;

public sealed class StreamProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChannelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TargetFps { get; set; } = 25;
    public int TargetBitrateKbps { get; set; } = 2048;
    public bool UseHardwareDecoding { get; set; } = true;
}
