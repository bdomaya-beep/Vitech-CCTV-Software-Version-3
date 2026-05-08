using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Runtime.InteropServices;
using OpenCvSharp;
using SentinelVMS.Application.Abstractions.Streaming;
using SentinelVMS.Streaming.Core;
using SentinelVMS.Streaming.Models;

namespace SentinelVMS.Streaming.Pipeline;

public sealed class StreamingOrchestrator(
    IRtspClient rtspClient,
    IDecoder decoder,
    IFrameSink frameSink) : IStreamingOrchestrator
{
    private sealed class ChannelRuntime
    {
        public required CancellationTokenSource Cancellation { get; init; }
        public Task NetworkTask { get; set; } = Task.CompletedTask;
        public Task[] DecoderTasks { get; set; } = [];
        public Task UploadTask { get; set; } = Task.CompletedTask;
        public Channel<MediaPacket> PacketQueue { get; } = Channel.CreateBounded<MediaPacket>(new BoundedChannelOptions(2048)
        {
            SingleReader = false,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        public Channel<VideoFrame> FrameQueue { get; } = Channel.CreateBounded<VideoFrame>(new BoundedChannelOptions(1024)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        public long FramesDecoded;
        public long BytesReceived;
        public DateTimeOffset LastMetricsUtc = DateTimeOffset.UtcNow;
        public LiveMetrics LastMetrics = new(0, 0, false);
    }

    private readonly ConcurrentDictionary<Guid, ChannelRuntime> _runtimes = new();

    public async Task StartChannelAsync(Guid channelId, string rtspUrl, bool lowQuality, CancellationToken cancellationToken = default)
    {
        if (_runtimes.ContainsKey(channelId))
        {
            return;
        }

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var runtime = BuildRuntime(channelId, rtspUrl, linkedCts);
        if (!_runtimes.TryAdd(channelId, runtime))
        {
            linkedCts.Cancel();
            return;
        }

        await Task.Yield();
    }

    public async Task StopChannelAsync(Guid channelId, CancellationToken cancellationToken = default)
    {
        if (!_runtimes.TryRemove(channelId, out var runtime))
        {
            return;
        }

        runtime.Cancellation.Cancel();
        await Task.WhenAll(runtime.DecoderTasks.Concat([runtime.NetworkTask, runtime.UploadTask]));
        runtime.Cancellation.Dispose();
    }

    public Task<LiveMetrics> GetMetricsAsync(Guid channelId, CancellationToken cancellationToken = default)
    {
        if (!_runtimes.TryGetValue(channelId, out var runtime))
        {
            return Task.FromResult(new LiveMetrics(0, 0, false));
        }

        var now = DateTimeOffset.UtcNow;
        var elapsed = (now - runtime.LastMetricsUtc).TotalSeconds;
        if (elapsed > 0.99)
        {
            var fps = runtime.FramesDecoded / elapsed;
            var kbps = (int)((runtime.BytesReceived * 8) / 1000d / elapsed);
            runtime.LastMetrics = new LiveMetrics(fps, kbps, true);
            runtime.LastMetricsUtc = now;
            Interlocked.Exchange(ref runtime.FramesDecoded, 0);
            Interlocked.Exchange(ref runtime.BytesReceived, 0);
        }

        return Task.FromResult(runtime.LastMetrics);
    }

    private ChannelRuntime BuildRuntime(Guid channelId, string rtspUrl, CancellationTokenSource linkedCts)
    {
        var runtime = new ChannelRuntime
        {
            Cancellation = linkedCts,
            NetworkTask = Task.CompletedTask,
            DecoderTasks = [],
            UploadTask = Task.CompletedTask
        };

        runtime.NetworkTask = Task.Run(async () =>
        {
            using var capture = new VideoCapture();
            capture.Open(rtspUrl, VideoCaptureAPIs.FFMPEG);
            if (!capture.IsOpened())
            {
                runtime.LastMetrics = new LiveMetrics(0, 0, false);
                return;
            }

            using var frame = new Mat();
            using var bgra = new Mat();

            while (!linkedCts.IsCancellationRequested)
            {
                if (!capture.Read(frame) || frame.Empty())
                {
                    await Task.Delay(20, linkedCts.Token);
                    continue;
                }

                Cv2.CvtColor(frame, bgra, ColorConversionCodes.BGR2BGRA);
                var size = bgra.Rows * bgra.Cols * bgra.ElemSize();
                var pixels = new byte[size];
                Marshal.Copy(bgra.Data, pixels, 0, size);

                var vf = new VideoFrame
                {
                    ChannelId = channelId,
                    Width = bgra.Cols,
                    Height = bgra.Rows,
                    Stride = bgra.Cols * 4,
                    PixelData = pixels
                };

                Interlocked.Increment(ref runtime.FramesDecoded);
                Interlocked.Add(ref runtime.BytesReceived, pixels.LongLength);
                await frameSink.OnFrameAsync(vf, linkedCts.Token);
            }
        }, linkedCts.Token);

        runtime.DecoderTasks = [];
        runtime.UploadTask = Task.CompletedTask;

        return runtime;
    }
}
