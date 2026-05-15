using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Runtime.InteropServices;
using OpenCvSharp;
using SentinelVMS.Application.Abstractions.Streaming;
using SentinelVMS.Streaming.Core;
using SentinelVMS.Streaming.Models;
using System.Threading;

namespace SentinelVMS.Streaming.Pipeline;

public sealed class StreamingOrchestrator(
    IRtspClient rtspClient,
    IDecoder decoder,
    IFrameSink frameSink) : IStreamingOrchestrator
{
    private const int CaptureBufferSizeProperty = 38;
    private static int _captureOptionsConfigured;

    private sealed class ChannelRuntime
    {
        public required string RtspUrl { get; init; }
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

        // 0 = pending, 1 = opened, -1 = failed
        public volatile int StreamState = 0;
        public DateTimeOffset StartedUtc = DateTimeOffset.UtcNow;
        public DateTimeOffset LastFrameUtc = DateTimeOffset.UtcNow;
    }

    private readonly ConcurrentDictionary<Guid, ChannelRuntime> _runtimes = new();

    public async Task StartChannelAsync(Guid channelId, string rtspUrl, bool lowQuality, CancellationToken cancellationToken = default)
    {
        if (_runtimes.TryGetValue(channelId, out var existingRuntime))
        {
            if (string.Equals(existingRuntime.RtspUrl, rtspUrl, StringComparison.Ordinal))
            {
                return;
            }

            // Same channel, different URL (sub/main switch): restart runtime so new stream takes effect.
            await StopChannelAsync(channelId, cancellationToken);
        }

        if (_runtimes.ContainsKey(channelId))
        {
            return;
        }

        ConfigureCaptureOptions();

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

        // Stream failed to open → report disconnected immediately
        if (runtime.StreamState == -1)
        {
            return Task.FromResult(new LiveMetrics(0, 0, false));
        }

        // Still waiting for RTSP open (grace period = 8 s)
        var age = (DateTimeOffset.UtcNow - runtime.StartedUtc).TotalSeconds;
        if (runtime.StreamState == 0)
        {
            var connecting = age < 5.0;
            return Task.FromResult(new LiveMetrics(0, 0, connecting));
        }

        var now = DateTimeOffset.UtcNow;
        var elapsed = (now - runtime.LastMetricsUtc).TotalSeconds;
        if (elapsed > 0.99)
        {
            var fps  = runtime.FramesDecoded / elapsed;
            var kbps = (int)((runtime.BytesReceived * 8) / 1000d / elapsed);
            var sinceLastFrame = (now - runtime.LastFrameUtc).TotalSeconds;
            bool connected = fps > 0 || sinceLastFrame < 1.5;
            runtime.LastMetrics = new LiveMetrics(fps, kbps, connected);
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
            RtspUrl = rtspUrl,
            Cancellation = linkedCts,
            NetworkTask = Task.CompletedTask,
            DecoderTasks = [],
            UploadTask = Task.CompletedTask,
            StartedUtc = DateTimeOffset.UtcNow,
            LastFrameUtc = DateTimeOffset.UtcNow
        };

        runtime.NetworkTask = Task.Run(async () =>
        {
            // Retry loop: attempt to open the stream up to 3 times before giving up
            const int maxRetries = 4;
            for (var attempt = 0; attempt < maxRetries; attempt++)
            {
                if (linkedCts.IsCancellationRequested) return;

                using var capture = new VideoCapture();
                runtime.StartedUtc = DateTimeOffset.UtcNow;
                runtime.LastFrameUtc = runtime.StartedUtc;

                // Keep the decoder close to real-time instead of draining a long RTSP buffer.
                capture.Set((VideoCaptureProperties)CaptureBufferSizeProperty, 1);
                capture.Open(rtspUrl, VideoCaptureAPIs.FFMPEG);
                capture.Set((VideoCaptureProperties)CaptureBufferSizeProperty, 1);

                if (!capture.IsOpened())
                {
                    runtime.StreamState = attempt < maxRetries - 1 ? 0 : -1;
                    if (attempt < maxRetries - 1)
                        await Task.Delay(1000, linkedCts.Token).ContinueWith(_ => { }); // swallow cancellation
                    continue;
                }

                runtime.StreamState = 1;

                using var frame = new Mat();
                using var bgra  = new Mat();
                var consecutiveEmptyReads = 0;

                while (!linkedCts.IsCancellationRequested)
                {
                    if (!capture.Read(frame) || frame.Empty())
                    {
                        consecutiveEmptyReads++;

                        // If the RTSP session opened but never starts delivering frames, restart it.
                        var openedButStalled = consecutiveEmptyReads >= 75 &&
                                               (DateTimeOffset.UtcNow - runtime.LastFrameUtc).TotalSeconds > 2.5;
                        if (openedButStalled)
                        {
                            runtime.StreamState = attempt < maxRetries - 1 ? 0 : -1;
                            break;
                        }

                        await Task.Delay(15, linkedCts.Token).ContinueWith(_ => { });
                        continue;
                    }

                    consecutiveEmptyReads = 0;
                    runtime.LastFrameUtc = DateTimeOffset.UtcNow;

                    Cv2.CvtColor(frame, bgra, ColorConversionCodes.BGR2BGRA);
                    var size   = bgra.Rows * bgra.Cols * bgra.ElemSize();
                    var pixels = new byte[size];
                    Marshal.Copy(bgra.Data, pixels, 0, size);

                    var vf = new VideoFrame
                    {
                        ChannelId  = channelId,
                        Width      = bgra.Cols,
                        Height     = bgra.Rows,
                        Stride     = bgra.Cols * 4,
                        PixelData  = pixels
                    };

                    Interlocked.Increment(ref runtime.FramesDecoded);
                    Interlocked.Add(ref runtime.BytesReceived, pixels.LongLength);
                    await frameSink.OnFrameAsync(vf, linkedCts.Token);
                }

                if (linkedCts.IsCancellationRequested)
                {
                    return;
                }

                if (attempt < maxRetries - 1)
                {
                    await Task.Delay(500, linkedCts.Token).ContinueWith(_ => { });
                    continue;
                }

                runtime.StreamState = -1;
                return;
            }
        }, linkedCts.Token);

        runtime.DecoderTasks = [];
        runtime.UploadTask = Task.CompletedTask;

        return runtime;
    }

    private static void ConfigureCaptureOptions()
    {
        if (Interlocked.Exchange(ref _captureOptionsConfigured, 1) == 1)
        {
            return;
        }

        // Applies to OpenCV's FFmpeg backend process-wide. Keeps RTSP playback closer to live.
        Environment.SetEnvironmentVariable(
            "OPENCV_FFMPEG_CAPTURE_OPTIONS",
            "rtsp_transport;tcp|max_delay;250000|fflags;nobuffer|flags;low_delay|buffer_size;65536");
    }
}
