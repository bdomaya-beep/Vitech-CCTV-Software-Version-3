using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SentinelVMS.Application.Abstractions.Devices;
using SentinelVMS.Application.Abstractions.Streaming;
using SentinelVMS.Domain.Models;
using SentinelVMS.Presentation.Core;
using System.Collections.ObjectModel;

namespace SentinelVMS.Presentation.ViewModels;

public partial class LiveViewViewModel(
    IDeviceService deviceService,
    IStreamingOrchestrator streamingOrchestrator) : ViewModelBase
{
    private readonly PeriodicTimer _metricsTimer = new(TimeSpan.FromSeconds(1));
    private bool _metricsLoopStarted;

    public ObservableCollection<LiveTileViewModel> Tiles { get; } = [];

    [ObservableProperty]
    private int _gridRows = 2;

    [ObservableProperty]
    private int _gridColumns = 2;

    [ObservableProperty]
    private bool _isFullscreen;

    [ObservableProperty]
    private bool _isSingleTileMode;

    [ObservableProperty]
    private LiveTileViewModel? _focusedTile;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await StopAllConnectedTilesAsync();
        Tiles.Clear();
        var devices = await deviceService.GetAllAsync();
        var channels = devices.SelectMany(x => x.Channels).ToList();

        foreach (var channel in channels)
        {
            Tiles.Add(LiveTileViewModel.FromChannel(channel));
        }

        EnsureGridCapacity(GridRows * GridColumns);

        if (!_metricsLoopStarted)
        {
            _metricsLoopStarted = true;
            _ = Task.Run(UpdateMetricsLoopAsync);
        }
    }

    public Task ReloadAsync() => LoadAsync();

    public async Task StopAllConnectedTilesAsync()
    {
        foreach (var tile in Tiles.Where(t => t.IsConnected && t.ChannelId != Guid.Empty))
        {
            try { await streamingOrchestrator.StopChannelAsync(tile.ChannelId); } catch { /* best effort */ }
            tile.IsConnected = false;
        }
    }

    public async Task AssignChannelToTileAsync(Guid channelId, string channelName, LiveTileViewModel targetTile)
    {
        // Ensure a channel can only exist in one tile at a time.
        var duplicates = Tiles.Where(t => !ReferenceEquals(t, targetTile) && t.ChannelId == channelId).ToList();
        foreach (var duplicate in duplicates)
        {
            if (duplicate.IsConnected)
            {
                await StopTileAsync(duplicate);
            }

            var slotIndex = Tiles.IndexOf(duplicate) + 1;
            duplicate.ChannelId = Guid.Empty;
            duplicate.Title = $"Slot {slotIndex}";
            duplicate.IsPlaceholder = true;
            duplicate.IsConnected = false;
            duplicate.Fps = 0;
            duplicate.BitrateKbps = 0;
            duplicate.ClearError();
        }

        if (targetTile.IsConnected && targetTile.ChannelId != Guid.Empty && targetTile.ChannelId != channelId)
        {
            await StopTileAsync(targetTile);
        }

        targetTile.ChannelId = channelId;
        targetTile.Title = channelName;
        targetTile.IsPlaceholder = false;
        targetTile.IsConnected = false;
        targetTile.Fps = 0;
        targetTile.BitrateKbps = 0;
        targetTile.ClearError();

        await StartTileAsync(targetTile);
    }

    public void ToggleSingleTileMode(LiveTileViewModel? tile)
    {
        if (tile is null || tile.IsPlaceholder)
        {
            return;
        }

        if (IsSingleTileMode && ReferenceEquals(FocusedTile, tile))
        {
            FocusedTile = null;
            IsSingleTileMode = false;
            return;
        }

        FocusedTile = tile;
        IsSingleTileMode = true;
    }

    [RelayCommand]
    private async Task StartTileAsync(LiveTileViewModel? tile)
    {
        if (tile is null || tile.ChannelId == Guid.Empty)
        {
            if (tile is not null)
            {
                tile.SetError("Assign a channel first (drag from tree).");
            }
            return;
        }

        var channel = await FindChannelAsync(tile.ChannelId);
        if (channel is null)
        {
            tile.SetError("Channel not found. Reload live view.");
            return;
        }

        try
        {
            await streamingOrchestrator.StartChannelAsync(channel.Id, channel.RtspSubstreamUrl, true);
            tile.IsConnected = true;
            tile.ClearError();
        }
        catch (Exception ex)
        {
            tile.IsConnected = false;
            tile.SetError($"Play failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task StopTileAsync(LiveTileViewModel? tile)
    {
        if (tile is null)
        {
            return;
        }

        try
        {
            await streamingOrchestrator.StopChannelAsync(tile.ChannelId);
            tile.IsConnected = false;
            tile.ClearError();
        }
        catch
        {
            tile.IsConnected = false;
        }
    }

    [RelayCommand]
    private void SetGrid(string? grid)
    {
        var normalized = grid ?? "2x2";
        var parts = normalized.Split('x');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var rows) || !int.TryParse(parts[1], out var cols))
        {
            return;
        }

        GridRows = rows;
        GridColumns = cols;
    }

    public void SetGridLayout(int gridSize)
    {
        GridRows = gridSize;
        GridColumns = gridSize;
        EnsureGridCapacity(GridRows * GridColumns);
    }

    private void EnsureGridCapacity(int desiredTiles)
    {
        var realTiles = Tiles.Count(x => x.ChannelId != Guid.Empty);
        var targetTiles = Math.Max(desiredTiles, realTiles);

        while (Tiles.Count < desiredTiles)
        {
            Tiles.Add(LiveTileViewModel.CreatePlaceholder(Tiles.Count + 1));
        }

        while (Tiles.Count > targetTiles)
        {
            var last = Tiles[^1];
            if (last.ChannelId != Guid.Empty)
            {
                break;
            }

            Tiles.RemoveAt(Tiles.Count - 1);
        }

        while (Tiles.Count < targetTiles)
        {
            Tiles.Add(LiveTileViewModel.CreatePlaceholder(Tiles.Count + 1));
        }
    }

    private async Task<CameraChannel?> FindChannelAsync(Guid channelId)
    {
        var devices = await deviceService.GetAllAsync();
        return devices.SelectMany(x => x.Channels).FirstOrDefault(c => c.Id == channelId);
    }

    private async Task UpdateMetricsLoopAsync()
    {
        while (await _metricsTimer.WaitForNextTickAsync())
        {
            foreach (var tile in Tiles.Where(x => x.IsConnected))
            {
                var metrics = await streamingOrchestrator.GetMetricsAsync(tile.ChannelId);
                tile.Fps = metrics.Fps;
                tile.BitrateKbps = metrics.BitrateKbps;
                tile.IsConnected = metrics.Connected;
            }
        }
    }
}
