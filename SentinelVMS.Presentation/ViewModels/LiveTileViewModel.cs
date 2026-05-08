using CommunityToolkit.Mvvm.ComponentModel;
using SentinelVMS.Domain.Models;

namespace SentinelVMS.Presentation.ViewModels;

public partial class LiveTileViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid _channelId;

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

    public static LiveTileViewModel FromChannel(CameraChannel channel)
    {
        return new LiveTileViewModel
        {
            ChannelId = channel.Id,
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
                Title = $"Slot {slot}",
            IsConnected = false,
            Fps = 0,
            BitrateKbps = 0,
                ErrorMessage = string.Empty,
                IsErrorVisible = false,
                IsPlaceholder = true
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
