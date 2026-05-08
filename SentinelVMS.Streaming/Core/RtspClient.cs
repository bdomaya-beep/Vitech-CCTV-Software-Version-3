using System.Net.Sockets;
using System.Runtime.CompilerServices;
using SentinelVMS.Streaming.Models;

namespace SentinelVMS.Streaming.Core;

public sealed class RtspClient : IRtspClient
{
    public async IAsyncEnumerable<MediaPacket> ReceivePacketsAsync(
        Guid channelId,
        string rtspUrl,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var uri = new Uri(rtspUrl);
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(uri.Host, uri.Port == -1 ? 554 : uri.Port, cancellationToken);

        using var stream = tcpClient.GetStream();
        var request = $"OPTIONS {rtspUrl} RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: SentinelVMS\r\n\r\n";
        var requestBytes = System.Text.Encoding.ASCII.GetBytes(request);
        await stream.WriteAsync(requestBytes, cancellationToken);

        var buffer = new byte[4096];
        while (!cancellationToken.IsCancellationRequested)
        {
            var bytes = await stream.ReadAsync(buffer, cancellationToken);
            if (bytes <= 0)
            {
                break;
            }

            var packet = new byte[bytes];
            Buffer.BlockCopy(buffer, 0, packet, 0, bytes);
            yield return new MediaPacket { ChannelId = channelId, Data = packet };
        }
    }
}
