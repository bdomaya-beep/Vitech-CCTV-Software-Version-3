using SentinelVMS.Domain.Enums;

namespace SentinelVMS.Domain.Models;

public sealed class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 554;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public DeviceHealthStatus HealthStatus { get; set; } = DeviceHealthStatus.Unknown;
    public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid? GroupId { get; set; }

    public DeviceGroup? Group { get; set; }
    public List<CameraChannel> Channels { get; set; } = [];

    public bool IsOnline => HealthStatus == DeviceHealthStatus.Online;
}
