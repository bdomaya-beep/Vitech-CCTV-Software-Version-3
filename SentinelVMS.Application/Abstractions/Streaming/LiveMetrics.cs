namespace SentinelVMS.Application.Abstractions.Streaming;

public sealed record LiveMetrics(double Fps, int BitrateKbps, bool Connected);
