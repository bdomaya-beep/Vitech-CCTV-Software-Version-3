using SentinelVMS.Domain.Enums;
using System.Windows.Media;

namespace SentinelVMS.Presentation.ViewModels;

public sealed class DeviceTreeItemViewModel(Guid id, string name, DeviceHealthStatus status, IReadOnlyList<DeviceTreeItemViewModel> children)
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public DeviceHealthStatus Status { get; } = status;
    public IReadOnlyList<DeviceTreeItemViewModel> Children { get; } = children;
    public bool IsExpanded { get; set; }
    public bool IsChannel { get; init; }
    public bool IsDevice => !IsChannel;

    public string StatusIndicator => Status switch
    {
        DeviceHealthStatus.Online   => "? Online",
        DeviceHealthStatus.Offline  => "? Offline",
        DeviceHealthStatus.Degraded => "? Degraded",
        _                           => "? Unknown"
    };

    public Brush StatusDotBrush => Status switch
    {
        DeviceHealthStatus.Online   => Brushes.LimeGreen,
        DeviceHealthStatus.Offline  => Brushes.IndianRed,
        DeviceHealthStatus.Degraded => Brushes.Goldenrod,
        _                           => Brushes.SlateGray
    };

    public string StatusLabel => Status switch
    {
        DeviceHealthStatus.Online   => "ONLINE",
        DeviceHealthStatus.Offline  => "OFFLINE",
        DeviceHealthStatus.Degraded => "DEGRADED",
        _                           => "UNKNOWN"
    };

    public string ChannelCountLabel => Children.Count > 0 ? $"{Children.Count} ch" : "";
}
