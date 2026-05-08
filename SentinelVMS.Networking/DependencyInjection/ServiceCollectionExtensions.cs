using Microsoft.Extensions.DependencyInjection;
using SentinelVMS.Networking.Discovery;
using SentinelVMS.Networking.Tcp;

namespace SentinelVMS.Networking.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNetworkingServices(this IServiceCollection services)
    {
        services.AddSingleton<IOnvifDiscoveryService, OnvifDiscoveryService>();
        services.AddSingleton<ITcpProbeService, TcpProbeService>();

        return services;
    }
}
