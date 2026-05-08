namespace SentinelVMS.Networking.Tcp;

public interface ITcpProbeService
{
    Task<bool> IsOpenAsync(string host, int port, int timeoutMs, CancellationToken cancellationToken = default);
}
