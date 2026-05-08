using Microsoft.Extensions.DependencyInjection;
using SentinelVMS.Application.Abstractions.Streaming;
using SentinelVMS.Streaming.Core;
using SentinelVMS.Streaming.Pipeline;

namespace SentinelVMS.Streaming.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStreamingServices(this IServiceCollection services)
    {
        services.AddSingleton<IRtspClient, RtspClient>();
        services.AddSingleton<IDecoder, FfmpegDecoder>();
        services.AddSingleton<IStreamingOrchestrator, StreamingOrchestrator>();

        return services;
    }
}
