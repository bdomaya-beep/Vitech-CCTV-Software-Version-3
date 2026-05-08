namespace SentinelVMS.Networking.Discovery;

public interface IOnvifDiscoveryService
{
    Task<IReadOnlyList<OnvifDiscoveryResult>> DiscoverAsync(CancellationToken cancellationToken = default);
}
