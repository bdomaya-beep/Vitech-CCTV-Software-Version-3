using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SentinelVMS.Application.Abstractions.Devices;
using SentinelVMS.Application.Abstractions.Streaming;
using SentinelVMS.Presentation.Core;
using System.Collections.ObjectModel;
using System.Globalization;

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

    public ObservableCollection<PlaybackChannelOption> Channels { get; } = [];

    [ObservableProperty]
    private PlaybackChannelOption? _selectedChannel;

    [ObservableProperty]
    private string _startLocalText = DateTime.Now.AddMinutes(-30).ToString("yyyy-MM-dd HH:mm:ss");

    [ObservableProperty]
    private string _endLocalText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    [ObservableProperty]
    private Guid _renderChannelId = PlaybackRenderChannelId;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _errorText = string.Empty;

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

        if (!TryParseLocalTime(StartLocalText, out var startUtc) || !TryParseLocalTime(EndLocalText, out var endUtc))
        {
            ErrorText = "Use format yyyy-MM-dd HH:mm:ss for start and end time.";
            return;
        }

        if (endUtc <= startUtc)
        {
            ErrorText = "End time must be greater than start time.";
            return;
        }

        try
        {
            await streamingOrchestrator.StopChannelAsync(PlaybackRenderChannelId);
        }
        catch
        {
            // Ignore if not running.
        }

        try
        {
            var playbackUrl = BuildDahuaPlaybackUrl(SelectedChannel, startUtc, endUtc);
            await streamingOrchestrator.StartChannelAsync(PlaybackRenderChannelId, playbackUrl, false);
            IsPlaying = true;
            StatusText = $"Playing {SelectedChannel.DisplayName}";
        }
        catch (Exception ex)
        {
            IsPlaying = false;
            ErrorText = $"Playback failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task StopPlaybackAsync()
    {
        try
        {
            await streamingOrchestrator.StopChannelAsync(PlaybackRenderChannelId);
        }
        catch
        {
            // Best effort stop.
        }

        IsPlaying = false;
        StatusText = "Playback stopped";
    }

    private static bool TryParseLocalTime(string value, out DateTimeOffset utc)
    {
        var formats = new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy/MM/dd HH:mm:ss", "yyyy/MM/dd HH:mm" };
        if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var local))
        {
            utc = default;
            return false;
        }

        utc = new DateTimeOffset(local).ToUniversalTime();
        return true;
    }

    private static string BuildDahuaPlaybackUrl(PlaybackChannelOption option, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        var start = startUtc.ToString("yyyy_MM_dd_HH_mm_ss", CultureInfo.InvariantCulture);
        var end = endUtc.ToString("yyyy_MM_dd_HH_mm_ss", CultureInfo.InvariantCulture);

        return $"rtsp://{Uri.EscapeDataString(option.Username)}:{Uri.EscapeDataString(option.Password)}@{option.Host}:554/cam/playback?channel={option.ChannelNumber}&subtype=0&starttime={start}&endtime={end}";
    }
}
