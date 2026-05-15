using CommunityToolkit.Mvvm.ComponentModel;
using SentinelVMS.Domain.Models;

namespace SentinelVMS.Presentation.ViewModels;

public partial class LiveTileViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid _channelId;

    [ObservableProperty]
    private Guid _renderChannelId;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private double _fps;

    [ObservableProperty]
    private int _bitrateKbps;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isErrorVisible;

    [ObservableProperty]
    private bool _isPlaceholder;

    // Set to true only after StartChannelAsync has actually been called for this tile
    public bool IsStreamStarted { get; set; }

    // Both stream URLs are stored at assignment time so single-tile mode can switch quality
    public string MainstreamUrl { get; set; } = string.Empty;
    public string SubstreamUrl { get; set; } = string.Empty;

    public static LiveTileViewModel FromChannel(CameraChannel channel)
    {
        return new LiveTileViewModel
        {
            ChannelId = channel.Id,
            RenderChannelId = channel.Id,
            Title = channel.Name,
            IsConnected = false,
            Fps = 0,
            BitrateKbps = 0,
            ErrorMessage = string.Empty,
                IsErrorVisible = false,
                IsPlaceholder = false
        };
    }

    public static LiveTileViewModel CreatePlaceholder(int slot)
    {
        return new LiveTileViewModel
        {
            ChannelId = Guid.Empty,
            RenderChannelId = Guid.Empty,
            Title = $"Slot {slot}",
            IsConnected = false,
            Fps = 0,
            BitrateKbps = 0,
            ErrorMessage = string.Empty,
            IsErrorVisible = false,
            IsPlaceholder = true,
            IsStreamStarted = false
        };
    }

    public LiveTileViewModel CreateFocusedClone()
    {
        return new LiveTileViewModel
        {
            ChannelId = ChannelId,
            RenderChannelId = Guid.NewGuid(),
            Title = Title,
            IsConnected = false,
            Fps = 0,
            BitrateKbps = 0,
            ErrorMessage = string.Empty,
            IsErrorVisible = false,
            IsPlaceholder = IsPlaceholder,
            IsStreamStarted = false,
            MainstreamUrl = MainstreamUrl,
            SubstreamUrl = SubstreamUrl
        };
    }

    public void SetError(string message)
    {
        ErrorMessage = message;
        IsErrorVisible = !string.IsNullOrWhiteSpace(message);
    }

    public void ClearError()
    {
        ErrorMessage = string.Empty;
        IsErrorVisible = false;
    }
}
