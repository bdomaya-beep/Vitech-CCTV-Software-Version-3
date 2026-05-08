using SentinelVMS.Application.Abstractions.Devices;
using SentinelVMS.Domain.Models;
using SentinelVMS.Networking.Discovery;
using SentinelVMS.Networking.Tcp;

namespace SentinelVMS.Infrastructure.Devices;

public sealed class DeviceDiscoveryService(
    IOnvifDiscoveryService onvifDiscoveryService,
    ITcpProbeService tcpProbeService) : IDeviceDiscoveryService
{
    public async Task<IReadOnlyList<Device>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var onvif = await onvifDiscoveryService.DiscoverAsync(cancellationToken);
        var devices = new List<Device>();

        foreach (var item in onvif)
        {
            var rtspOpen = await tcpProbeService.IsOpenAsync(item.Host, 554, 500, cancellationToken);
            devices.Add(new Device
            {
                Name = $"{item.Manufacturer} {item.Model} {item.Host}",
                Host = item.Host,
                Port = rtspOpen ? 554 : item.Port,
                Manufacturer = item.Manufacturer,
                Model = item.Model
            });
        }

        return devices;
    }
}
