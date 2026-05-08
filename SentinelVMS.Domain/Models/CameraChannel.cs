namespace SentinelVMS.Domain.Models;

public sealed class CameraChannel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public int ChannelNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RtspMainstreamUrl { get; set; } = string.Empty;
    public string RtspSubstreamUrl { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    public Device? Device { get; set; }
}
