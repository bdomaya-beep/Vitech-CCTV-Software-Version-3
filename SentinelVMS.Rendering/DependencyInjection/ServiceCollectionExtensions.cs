using Microsoft.Extensions.DependencyInjection;
using SentinelVMS.Rendering.Core;

namespace SentinelVMS.Rendering.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRenderingServices(this IServiceCollection services)
    {
        services.AddSingleton<IDirectXRenderer, DirectX11Renderer>();

        return services;
    }
}
