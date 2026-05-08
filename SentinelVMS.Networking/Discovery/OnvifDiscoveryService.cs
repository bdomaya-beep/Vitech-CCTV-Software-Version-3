using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace SentinelVMS.Networking.Discovery;

public sealed class OnvifDiscoveryService : IOnvifDiscoveryService
{
    private static readonly Regex XAddressRegex = new("http://(?<host>[^:/]+)(:(?<port>\\d+))?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<IReadOnlyList<OnvifDiscoveryResult>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        using var client = new UdpClient();
        client.EnableBroadcast = true;
        client.Client.ReceiveTimeout = 1200;

        var probe = """
<?xml version=\"1.0\" encoding=\"UTF-8\"?>
<e:Envelope xmlns:e=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:w=\"http://schemas.xmlsoap.org/ws/2004/08/addressing\" xmlns:d=\"http://schemas.xmlsoap.org/ws/2005/04/discovery\" xmlns:dn=\"http://www.onvif.org/ver10/network/wsdl\">
  <e:Header>
    <w:MessageID>uuid:6f4e0f95-b6b5-4b90-a7e7-1f72df1ff200</w:MessageID>
    <w:To>urn:schemas-xmlsoap-org:ws:2005:04:discovery</w:To>
    <w:Action>http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</w:Action>
  </e:Header>
  <e:Body>
    <d:Probe><d:Types>dn:NetworkVideoTransmitter</d:Types></d:Probe>
  </e:Body>
</e:Envelope>
""";

        var payload = Encoding.UTF8.GetBytes(probe);
        await client.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 3702));

        var results = new List<OnvifDiscoveryResult>();
        var stopAt = DateTime.UtcNow.AddSeconds(2);

        while (DateTime.UtcNow < stopAt && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var receiveTask = client.ReceiveAsync(cancellationToken);
                var response = await receiveTask;
                var body = Encoding.UTF8.GetString(response.Buffer);
                var match = XAddressRegex.Match(body);
                if (!match.Success)
                {
                    continue;
                }

                var host = match.Groups["host"].Value;
                var port = int.TryParse(match.Groups["port"].Value, out var parsedPort) ? parsedPort : 80;
                if (results.Any(x => x.Host == host && x.Port == port))
                {
                    continue;
                }

                results.Add(new OnvifDiscoveryResult(host, port, "ONVIF", "NVT"));
            }
            catch
            {
                break;
            }
        }

        return results;
    }
}
