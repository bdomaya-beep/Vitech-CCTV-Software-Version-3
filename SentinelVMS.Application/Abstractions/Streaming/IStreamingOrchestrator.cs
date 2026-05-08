namespace SentinelVMS.Application.Abstractions.Streaming;

public interface IStreamingOrchestrator
{
    Task StartChannelAsync(Guid channelId, string rtspUrl, bool lowQuality, CancellationToken cancellationToken = default);
    Task StopChannelAsync(Guid channelId, CancellationToken cancellationToken = default);
    Task<LiveMetrics> GetMetricsAsync(Guid channelId, CancellationToken cancellationToken = default);
}
