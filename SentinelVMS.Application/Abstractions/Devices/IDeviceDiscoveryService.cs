using SentinelVMS.Domain.Models;

namespace SentinelVMS.Application.Abstractions.Devices;

public interface IDeviceDiscoveryService
{
    Task<IReadOnlyList<Device>> DiscoverAsync(CancellationToken cancellationToken = default);
}
