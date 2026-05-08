namespace SentinelVMS.Networking.Discovery;

public sealed record OnvifDiscoveryResult(string Host, int Port, string Manufacturer, string Model);
