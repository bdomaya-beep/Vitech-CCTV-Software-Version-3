using System.Net.Sockets;

namespace SentinelVMS.Networking.Tcp;

public sealed class TcpProbeService : ITcpProbeService
{
    public async Task<bool> IsOpenAsync(string host, int port, int timeoutMs, CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(timeoutMs);
        try
        {
            await client.ConnectAsync(host, port, linkedCts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
