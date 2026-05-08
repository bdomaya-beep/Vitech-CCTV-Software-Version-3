using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SentinelVMS.Application.Abstractions.Devices;
using SentinelVMS.Application.DTOs;
using SentinelVMS.Domain.Models;
using SentinelVMS.Presentation.Core;
using System.Collections.ObjectModel;

namespace SentinelVMS.Presentation.ViewModels;

public partial class DeviceManagementViewModel(
    IDeviceService deviceService,
    IDeviceDiscoveryService deviceDiscoveryService) : ViewModelBase
{
    private const int NvrPort = 37777;

    public event Action? DevicesChanged;

    public ObservableCollection<Device> Devices { get; } = [];

    [ObservableProperty]
    private string _newDeviceName = "NVR 1";

    [ObservableProperty]
    private string _newDeviceHost = "192.168.1.100";

    [ObservableProperty]
    private int _newDevicePort = NvrPort;

    [ObservableProperty]
    private string _newDeviceUsername = "admin";

    [ObservableProperty]
    private string _newDevicePassword = string.Empty;

    [ObservableProperty]
    private string _newDeviceManufacturer = "Dahua";

    [ObservableProperty]
    private string _newDeviceModel = "NVR";

    [ObservableProperty]
    private Device? _selectedDevice;

    [ObservableProperty]
    private bool _isDiscovering;

    [ObservableProperty]
    private CameraChannel? _selectedChannel;

    [RelayCommand]
    private async Task LoadAsync()
    {
        Devices.Clear();
        var devices = await deviceService.GetAllAsync();
        foreach (var device in devices)
        {
            Devices.Add(device);
        }

        DevicesChanged?.Invoke();
    }

    [RelayCommand]
    private async Task DiscoverAsync()
    {
        IsDiscovering = true;
        var discovered = await deviceDiscoveryService.DiscoverAsync();
        foreach (var item in discovered)
        {
            if (Devices.Any(x => x.Host == item.Host && x.Port == item.Port))
            {
                continue;
            }

            var created = await deviceService.AddAsync(new DeviceUpsertRequest(item.Name, item.Host, item.Port, item.Username, item.Password, item.Manufacturer, item.Model));
            Devices.Add(created);
        }

        IsDiscovering = false;
        DevicesChanged?.Invoke();
    }

    [RelayCommand]
    private async Task AddManualDeviceAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDeviceName) || string.IsNullOrWhiteSpace(NewDeviceHost))
        {
            return;
        }

        NewDevicePort = NvrPort;

        var request = new DeviceUpsertRequest(
            NewDeviceName.Trim(),
            NewDeviceHost.Trim(),
            NewDevicePort,
            NewDeviceUsername.Trim(),
            NewDevicePassword,
            NewDeviceManufacturer.Trim(),
            NewDeviceModel.Trim(),
            0,
            true);

        var created = await deviceService.AddAsync(request);
        Devices.Add(created);

        NewDeviceName = $"NVR {Devices.Count + 1}";
        DevicesChanged?.Invoke();
    }

    [RelayCommand]
    private async Task TestSelectedConnectionAsync()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        var updated = await deviceService.TestConnectionAsync(SelectedDevice.Id);
        ReplaceDeviceInCollection(updated);
        SelectedDevice = updated;
        DevicesChanged?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedDevice))]
    private async Task RefreshChannelNamesAsync()
    {
        if (SelectedDevice is null) return;

        await deviceService.RefreshChannelNamesAsync(SelectedDevice.Id);
        // Reload to show updated channel names
        await LoadCommand.ExecuteAsync(null);
        DevicesChanged?.Invoke();
    }

    [RelayCommand]
    private async Task RenameChannelAsync(CameraChannel? channel)
    {
        if (channel is null)
        {
            return;
        }

        var normalizedName = channel.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return;
        }

        await deviceService.RenameChannelAsync(channel.Id, normalizedName);
        await LoadCommand.ExecuteAsync(null);

        if (SelectedDevice is not null)
        {
            SelectedDevice = Devices.FirstOrDefault(x => x.Id == SelectedDevice.Id);
        }

        DevicesChanged?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedDevice))]
    private async Task DeleteSelectedDeviceAsync()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        var deviceId = SelectedDevice.Id;
        await deviceService.DeleteAsync(deviceId);

        var existing = Devices.FirstOrDefault(x => x.Id == deviceId);
        if (existing is not null)
        {
            Devices.Remove(existing);
        }

        SelectedDevice = null;
        DevicesChanged?.Invoke();
    }

    private bool CanDeleteSelectedDevice() => SelectedDevice is not null;
    private bool HasSelectedDevice() => SelectedDevice is not null;

    partial void OnSelectedDeviceChanged(Device? value)
    {
        DeleteSelectedDeviceCommand.NotifyCanExecuteChanged();
        RefreshChannelNamesCommand.NotifyCanExecuteChanged();
    }

    private void ReplaceDeviceInCollection(Device updated)
    {
        for (var i = 0; i < Devices.Count; i++)
        {
            if (Devices[i].Id == updated.Id)
            {
                Devices[i] = updated;
                return;
            }
        }
    }
}
