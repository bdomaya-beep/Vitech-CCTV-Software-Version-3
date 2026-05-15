using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SentinelVMS.Application.Abstractions.Authentication;
using SentinelVMS.Application.Abstractions.Devices;
using SentinelVMS.Domain.Enums;
using SentinelVMS.Presentation.Core;
using System.Collections.ObjectModel;

namespace SentinelVMS.Presentation.ViewModels;

public partial class ShellViewModel(
    INavigationService navigationService,
    LiveViewViewModel liveViewViewModel,
    DeviceManagementViewModel deviceManagementViewModel,
    PlaybackViewModel playbackViewModel,
    ISessionService sessionService,
    IAuthenticationService authenticationService,
    IDeviceService deviceService) : ViewModelBase
{
    public event Action? RequestOpenLivePopout;

    public ObservableCollection<DeviceTreeItemViewModel> DeviceTree { get; } = [];
    public LiveViewViewModel LiveViewModel => liveViewViewModel;
    public DeviceManagementViewModel DeviceManagementViewModel => deviceManagementViewModel;
    public PlaybackViewModel PlaybackViewModel => playbackViewModel;

    [ObservableProperty]
    private string _currentUser = "";

    [ObservableProperty]
    private string _statusText = "System ready";

    [ObservableProperty]
    private string _metricsText = "";

    [ObservableProperty]
    private int _connectedDeviceCount = 0;

    [ObservableProperty]
    private bool _isFullscreen;

    [ObservableProperty]
    private object? _currentWorkspace;

    public async Task InitializeAsync()
    {
        deviceManagementViewModel.DevicesChanged += OnDevicesChanged;

        CurrentUser = sessionService.CurrentSession?.Username ?? "Unknown";
        await deviceManagementViewModel.LoadCommand.ExecuteAsync(null);
        await liveViewViewModel.LoadCommand.ExecuteAsync(null);
        await playbackViewModel.LoadChannelsCommand.ExecuteAsync(null);
        await RefreshTreeAsync();

        CurrentWorkspace = liveViewViewModel;
        navigationService.Navigate(liveViewViewModel);
        
        // Start metrics update loop
        _ = UpdateMetricsAsync();
    }

    [RelayCommand]
    private async Task OpenLiveViewAsync()
    {
        await liveViewViewModel.ReloadAsync();
        CurrentWorkspace = liveViewViewModel;
        navigationService.Navigate(liveViewViewModel);
        StatusText = "Live view workspace";
    }

    [RelayCommand]
    private void OpenDevices()
    {
        CurrentWorkspace = deviceManagementViewModel;
        navigationService.Navigate(deviceManagementViewModel);
        StatusText = "Device management workspace";
    }

    [RelayCommand]
    private async Task OpenPlaybackAsync()
    {
        await playbackViewModel.LoadChannelsCommand.ExecuteAsync(null);
        CurrentWorkspace = playbackViewModel;
        navigationService.Navigate(playbackViewModel);
        StatusText = "Playback workspace";
    }

    [RelayCommand]
    private async Task DiscoverDevicesAsync()
    {
        CurrentWorkspace = deviceManagementViewModel;
        navigationService.Navigate(deviceManagementViewModel);
        StatusText = "Discovering devices...";
        await deviceManagementViewModel.DiscoverCommand.ExecuteAsync(null);
        await RefreshTreeAsync();
        await liveViewViewModel.ReloadAsync();
        StatusText = "Auto discovery completed";
    }

    [RelayCommand]
    private void AddDevice()
    {
        StatusText = "Opening device management to add new device...";
        CurrentWorkspace = deviceManagementViewModel;
        navigationService.Navigate(deviceManagementViewModel);
    }

    [RelayCommand]
    private void SetGridLayout(string layoutStr)
    {
        if (int.TryParse(layoutStr, out int tileCount))
        {
            int gridSize = (int)Math.Sqrt(tileCount);
            liveViewViewModel.SetGridLayout(gridSize);
            CurrentWorkspace = liveViewViewModel;
            navigationService.Navigate(liveViewViewModel);
            StatusText = $"Grid layout: {gridSize}x{gridSize}";
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        StatusText = "Settings module is coming soon";
    }

    [RelayCommand]
    private void OpenHelp()
    {
        StatusText = "Help module is coming soon";
    }

    [RelayCommand]
    private void ToggleFullscreen()
    {
        IsFullscreen = !IsFullscreen;
    }

    [RelayCommand]
    private void OpenLivePopout()
    {
        CurrentWorkspace = liveViewViewModel;
        navigationService.Navigate(liveViewViewModel);
        StatusText = "Opened detachable live view window";
        RequestOpenLivePopout?.Invoke();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await authenticationService.LogoutAsync();
        StatusText = "Logged out";
    }

    private async Task RefreshTreeAsync()
    {
        DeviceTree.Clear();
        var devices = await deviceService.GetAllAsync();
        ConnectedDeviceCount = devices.Count(d => d.IsOnline);

        foreach (var device in devices)
        {
            var channels = device.Channels
                .OrderBy(c => c.ChannelNumber)
                .Select(c => new DeviceTreeItemViewModel(c.Id, c.Name, device.HealthStatus, [])
                {
                    IsChannel = true,
                    SubstreamUrl = c.RtspSubstreamUrl,
                    MainstreamUrl = c.RtspMainstreamUrl
                })
                .ToList();

            DeviceTree.Add(new DeviceTreeItemViewModel(device.Id, device.Name, device.HealthStatus, channels));
        }

        if (DeviceTree.Count == 0)
        {
            DeviceTree.Add(new DeviceTreeItemViewModel(Guid.Empty, "No devices", DeviceHealthStatus.Unknown, []));
        }
    }

    private async Task UpdateMetricsAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(1000);
                var devices = await deviceService.GetAllAsync();
                var onlineCount = devices.Count(d => d.IsOnline);
                var totalCount = devices.Count;
                
                ConnectedDeviceCount = onlineCount;
                MetricsText = $"{onlineCount}/{totalCount} devices online";
            }
            catch
            {
                // Silent fail for metrics updates
            }
        }
    }

    private void OnDevicesChanged()
    {
        _ = RefreshViewsAfterDeviceChangeAsync();
    }

    private async Task RefreshViewsAfterDeviceChangeAsync()
    {
        await RefreshTreeAsync();
        await liveViewViewModel.ReloadAsync();
        await playbackViewModel.LoadChannelsCommand.ExecuteAsync(null);
    }
}
