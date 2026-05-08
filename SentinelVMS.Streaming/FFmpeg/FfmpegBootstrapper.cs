using FFmpeg.AutoGen;

namespace SentinelVMS.Streaming.FFmpeg;

public static class FfmpegBootstrapper
{
    private static int _initialized;
    public static bool IsAvailable { get; private set; }

    public static void Initialize(string binariesPath)
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        try
        {
            ffmpeg.RootPath = binariesPath;
            ffmpeg.avdevice_register_all();
            ffmpeg.avformat_network_init();
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            // FFmpeg is optional - app can run without it, just without streaming
            System.Diagnostics.Debug.WriteLine($"FFmpeg initialization failed (non-fatal): {ex.Message}");
            IsAvailable = false;
        }
    }
}
