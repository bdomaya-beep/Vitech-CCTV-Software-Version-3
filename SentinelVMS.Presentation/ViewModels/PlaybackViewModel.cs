using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SentinelVMS.Application.Abstractions.Devices;
using SentinelVMS.Application.Abstractions.Streaming;
using SentinelVMS.Presentation.Core;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;

namespace SentinelVMS.Presentation.ViewModels;

public sealed record PlaybackChannelOption(
    Guid ChannelId,
    int ChannelNumber,
    string ChannelName,
    string DeviceName,
    string Host,
    string Username,
    string Password)
{
    public string DisplayName => $"{DeviceName} / CH{ChannelNumber} - {ChannelName}";
}

public partial class PlaybackViewModel(
    IDeviceService deviceService,
    IStreamingOrchestrator streamingOrchestrator) : ViewModelBase
{
    private static readonly Guid PlaybackRenderChannelId = new("7ca1ac53-4fd0-4bcc-89ef-2abfd3f4f0b1");
    private static readonly double[] SpeedSteps = { 0.25, 0.5, 1.0, 2.0, 4.0, 8.0 };

    private int _speedIndex = 2; // 1× by default
    private DateTimeOffset _playbackStartUtc;
    private DateTimeOffset _playbackEndUtc;
    private DateTime _positionWallClock;
    private bool _timerInitialized;
    private readonly DispatcherTimer _progressTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    public ObservableCollection<PlaybackChannelOption> Channels { get; } = [];

    [ObservableProperty]
    private PlaybackChannelOption? _selectedChannel;

    // ── Date / time fields (split for visibility) ──────────────────────────

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private string _startTimeText = DateTime.Now.AddMinutes(-30).ToString("HH:mm:ss");

    [ObservableProperty]
    private string _endTimeText = DateTime.Now.ToString("HH:mm:ss");

    // ── Rendering ──────────────────────────────────────────────────────────

    [ObservableProperty]
    private Guid _renderChannelId = PlaybackRenderChannelId;

    // ── Playback state ─────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _errorText = string.Empty;

    // ── Timeline ───────────────────────────────────────────────────────────

    [ObservableProperty]
    private double _playbackPosition = 0;

    [ObservableProperty]
    private string _currentPositionText = "--:--:--";

    [ObservableProperty]
    private string _rangeStartLabel = "--:--:--";

    [ObservableProperty]
    private string _rangeEndLabel = "--:--:--";

    // ── Transport controls ─────────────────────────────────────────────────

    [ObservableProperty]
    private string _playPauseContent = "▶  Play";

    public string SpeedText => $"{SpeedSteps[_speedIndex]:0.##}×";

    // ══════════════════════════════════════════════════════════════════════
    //  Commands
    // ══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    public async Task LoadChannelsAsync()
    {
        Channels.Clear();

        var devices = await deviceService.GetAllAsync();
        foreach (var device in devices.OrderBy(d => d.Name))
        {
            foreach (var channel in device.Channels.OrderBy(c => c.ChannelNumber))
            {
                Channels.Add(new PlaybackChannelOption(
                    channel.Id,
                    channel.ChannelNumber,
                    channel.Name,
                    device.Name,
                    device.Host,
                    device.Username,
                    device.Password));
            }
        }

        SelectedChannel = Channels.FirstOrDefault();
        StatusText = Channels.Count == 0 ? "No channels available" : "Choose a channel and time range";
    }

    [RelayCommand]
    public async Task StartPlaybackAsync()
    {
        ErrorText = string.Empty;

        if (SelectedChannel is null)
        {
            ErrorText = "Select a channel first.";
            return;
        }

        if (!TryBuildDateTime(SelectedDate, StartTimeText, out var startLocal) ||
            !TryBuildDateTime(SelectedDate, EndTimeText, out var endLocal))
        {
            ErrorText = "Use HH:mm:ss for start and end time.";
            return;
        }

        var startUtc = new DateTimeOffset(startLocal).ToUniversalTime();
        var endUtc   = new DateTimeOffset(endLocal).ToUniversalTime();

        if (endUtc <= startUtc)
        {
            ErrorText = "End time must be after start time.";
            return;
        }

        _progressTimer.Stop();
        if (!_timerInitialized) { _progressTimer.Tick += OnProgressTick; _timerInitialized = true; }

        try { await streamingOrchestrator.StopChannelAsync(PlaybackRenderChannelId); }
        catch { /* ignore if not running */ }

        try
        {
            var playbackUrl = BuildDahuaPlaybackUrl(SelectedChannel, startUtc, endUtc);
            await streamingOrchestrator.StartChannelAsync(PlaybackRenderChannelId, playbackUrl, false);

            _playbackStartUtc = startUtc;
            _playbackEndUtc   = endUtc;
            _positionWallClock = DateTime.UtcNow;

            RangeStartLabel     = startLocal.ToString("HH:mm:ss");
            RangeEndLabel       = endLocal.ToString("HH:mm:ss");
            PlaybackPosition    = 0;
            CurrentPositionText = startLocal.ToString("yyyy-MM-dd  HH:mm:ss");
            IsPlaying           = true;
            PlayPauseContent    = "⏸  Pause";
            StatusText          = $"Playing  {SelectedChannel.DisplayName}";

            _progressTimer.Start();
        }
        catch (Exception ex)
        {
            _progressTimer.Stop();
            IsPlaying        = false;
            PlayPauseContent = "▶  Play";
            ErrorText        = $"Playback failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task StopPlaybackAsync()
    {
        _progressTimer.Stop();

        try { await streamingOrchestrator.StopChannelAsync(PlaybackRenderChannelId); }
        catch { /* best-effort */ }

        IsPlaying           = false;
        PlayPauseContent    = "▶  Play";
        PlaybackPosition    = 0;
        CurrentPositionText = "--:--:--";
        StatusText          = "Stopped";
    }

    [RelayCommand]
    public async Task PlayPauseAsync()
    {
        if (IsPlaying)
            await StopPlaybackAsync();
        else
            await StartPlaybackAsync();
    }

    [RelayCommand]
    public void SpeedUp()
    {
        if (_speedIndex < SpeedSteps.Length - 1)
            _speedIndex++;
        OnPropertyChanged(nameof(SpeedText));
    }

    [RelayCommand]
    public void SlowDown()
    {
        if (_speedIndex > 0)
            _speedIndex--;
        OnPropertyChanged(nameof(SpeedText));
    }

    // ══════════════════════════════════════════════════════════════════════
    //  PlaybackPosition → seek when user drags the slider
    // ══════════════════════════════════════════════════════════════════════

    partial void OnPlaybackPositionChanged(double value)
    {
        if (!IsPlaying) return;

        var total    = (_playbackEndUtc - _playbackStartUtc).TotalSeconds;
        var seekSecs = total * (value / 100.0);
        var seekPos  = _playbackStartUtc.AddSeconds(seekSecs).ToLocalTime();
        CurrentPositionText = seekPos.ToString("yyyy-MM-dd  HH:mm:ss");
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Progress timer tick
    // ══════════════════════════════════════════════════════════════════════

    private void OnProgressTick(object? sender, EventArgs e)
    {
        if (!IsPlaying) return;

        var total     = (_playbackEndUtc - _playbackStartUtc).TotalSeconds;
        var speed     = SpeedSteps[_speedIndex];
        var elapsed   = (DateTime.UtcNow - _positionWallClock).TotalSeconds * speed;
        var pct       = total > 0 ? Math.Min(elapsed / total * 100.0, 100.0) : 0;

        var seekPos = _playbackStartUtc.AddSeconds(elapsed).ToLocalTime();
        CurrentPositionText = seekPos.ToString("yyyy-MM-dd  HH:mm:ss");
        PlaybackPosition    = pct;

        if (pct >= 100)
        {
            _progressTimer.Stop();
            IsPlaying        = false;
            PlayPauseContent = "▶  Play";
            StatusText       = "Playback finished";
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════════════

    private static bool TryBuildDateTime(DateTime date, string timeText, out DateTime result)
    {
        var formats = new[] { "HH:mm:ss", "HH:mm", "H:mm:ss", "H:mm" };
        if (!DateTime.TryParseExact(timeText.Trim(), formats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            result = default;
            return false;
        }

        result = date.Date.Add(time.TimeOfDay);
        return true;
    }

    private static string BuildDahuaPlaybackUrl(PlaybackChannelOption option, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        var start = startUtc.ToString("yyyy_MM_dd_HH_mm_ss", CultureInfo.InvariantCulture);
        var end   = endUtc.ToString("yyyy_MM_dd_HH_mm_ss",   CultureInfo.InvariantCulture);

        return $"rtsp://{Uri.EscapeDataString(option.Username)}:{Uri.EscapeDataString(option.Password)}@{option.Host}:554/cam/playback?channel={option.ChannelNumber}&subtype=0&starttime={start}&endtime={end}";
    }
}
