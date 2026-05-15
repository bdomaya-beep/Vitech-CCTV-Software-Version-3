using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SentinelVMS.Application.Abstractions.Devices;
using SentinelVMS.Application.Abstractions.Streaming;
using SentinelVMS.Domain.Models;
using SentinelVMS.Presentation.Core;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace SentinelVMS.Presentation.ViewModels;

public partial class LiveViewViewModel(
    IDeviceService deviceService,
    IStreamingOrchestrator streamingOrchestrator) : ViewModelBase
{
    private readonly DispatcherTimer _metricsTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromSeconds(1)
    };
    private bool _metricsLoopStarted;
    private readonly Dictionary<Guid, CameraChannel> _channelIndex = [];

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
        RebuildChannelIndex(devices);

        // Start with a clean grid of placeholders; user drags channels onto them
        EnsureGridCapacity(GridRows * GridColumns);

        if (!_metricsLoopStarted)
        {
            _metricsLoopStarted = true;
            _metricsTimer.Tick += MetricsTimer_OnTick;
            _metricsTimer.Start();
        }
    }

    public Task ReloadAsync() => LoadAsync();

    public async Task StopAllConnectedTilesAsync()
    {
        foreach (var tile in Tiles.Where(t => t.IsStreamStarted && t.ChannelId != Guid.Empty))
        {
            try { await streamingOrchestrator.StopChannelAsync(tile.RenderChannelId); } catch { /* best effort */ }
            tile.IsConnected = false;
            tile.IsStreamStarted = false;
        }

        if (FocusedTile is { IsStreamStarted: true, RenderChannelId: var focusedRenderId } && focusedRenderId != Guid.Empty)
        {
            try { await streamingOrchestrator.StopChannelAsync(focusedRenderId); } catch { /* best effort */ }
            FocusedTile.IsConnected = false;
            FocusedTile.IsStreamStarted = false;
        }

        FocusedTile = null;
        IsSingleTileMode = false;
    }

    public async Task AssignDeviceToGridAsync(IReadOnlyList<DeviceTreeItemViewModel> channels, LiveTileViewModel startTile)
    {
        if (channels.Count == 0) return;

        var channelItems = channels.Where(c => c.IsChannel).ToList();
        if (channelItems.Count == 0) return;

        // Resolve all dragged channels in one fetch to avoid repeated DB scans per tile.
        var devices = await deviceService.GetAllAsync();
        RebuildChannelIndex(devices);

        var startIndex = Tiles.IndexOf(startTile);
        if (startIndex < 0) startIndex = 0;

        // Expand grid if necessary so all channels fit
        var neededTiles = startIndex + channelItems.Count;
        if (neededTiles > Tiles.Count)
        {
            var newGrid = (int)Math.Ceiling(Math.Sqrt(neededTiles));
            if (newGrid > GridRows || newGrid > GridColumns)
            {
                GridRows = newGrid;
                GridColumns = newGrid;
            }
            EnsureGridCapacity(neededTiles);
        }

        for (var i = 0; i < channelItems.Count; i++)
        {
            var tileIndex = startIndex + i;
            if (tileIndex >= Tiles.Count) break;

            var ch = channelItems[i];
            var tile = Tiles[tileIndex];
            _channelIndex.TryGetValue(ch.Id, out var channel);

            try
            {
                await AssignChannelToTileAsync(ch.Id, ch.Name, tile,
                    channel?.RtspSubstreamUrl, channel?.RtspMainstreamUrl);
            }
            catch (Exception ex)
            {
                tile.SetError($"Play failed: {ex.Message}");
            }

            // Prevent RTSP burst-open storms when dropping an entire NVR.
            if (i < channelItems.Count - 1)
            {
                await Task.Delay(180);
            }
        }
    }

    public async Task AssignChannelToTileAsync(Guid channelId, string channelName, LiveTileViewModel targetTile, string? subUrl = null, string? mainUrl = null)
    {
        // Ensure a channel can only exist in one tile at a time.
        var duplicates = Tiles.Where(t => !ReferenceEquals(t, targetTile) && t.ChannelId == channelId).ToList();
        foreach (var duplicate in duplicates)
        {
            if (duplicate.IsStreamStarted)
            {
                await StopTileAsync(duplicate);
            }

            var slotIndex = Tiles.IndexOf(duplicate) + 1;
            duplicate.ChannelId = Guid.Empty;
            duplicate.RenderChannelId = Guid.Empty;
            duplicate.Title = $"Slot {slotIndex}";
            duplicate.IsPlaceholder = true;
            duplicate.IsConnected = false;
            duplicate.Fps = 0;
            duplicate.BitrateKbps = 0;
            duplicate.ClearError();
        }

        if (targetTile.IsStreamStarted && targetTile.ChannelId != Guid.Empty && targetTile.ChannelId != channelId)
        {
            await StopTileAsync(targetTile);
        }

        targetTile.ChannelId = channelId;
    targetTile.RenderChannelId = channelId;
        targetTile.Title = channelName;
        targetTile.IsPlaceholder = false;
        targetTile.IsConnected = false;
        targetTile.Fps = 0;
        targetTile.BitrateKbps = 0;
        targetTile.ClearError();

        if (!string.IsNullOrWhiteSpace(subUrl) || !string.IsNullOrWhiteSpace(mainUrl))
        {
            targetTile.SubstreamUrl = subUrl ?? string.Empty;
            targetTile.MainstreamUrl = mainUrl ?? string.Empty;
            await StartGridStreamAsync(targetTile);
            return;
        }

        await StartTileAsync(targetTile);
    }

    public async Task ToggleSingleTileMode(LiveTileViewModel? tile)
    {
        // Exit path — called with null (from the "Grid View" button) or with the same tile (double-click again)
        if (IsSingleTileMode && (tile is null || (FocusedTile is not null && tile?.ChannelId == FocusedTile.ChannelId)))
        {
            var prev = FocusedTile;
            FocusedTile = null;
            IsSingleTileMode = false;
            // Stop old stream in background — don't await so UI exits instantly
            if (prev is not null)
                _ = StopTileAsync(prev);
            return;
        }

        if (tile is null || tile.IsPlaceholder)
            return;

        // Swap to new tile immediately so UI shows new camera at once — stop old in background
        var oldFocused = FocusedTile;
        var newFocused = tile.CreateFocusedClone();
        FocusedTile = newFocused;
        IsSingleTileMode = true;

        if (oldFocused is not null)
            _ = StopTileAsync(oldFocused);

        // Start mainstream for focused view
        await StartFocusedStreamAsync(newFocused);
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

        // Cache both URLs now that we have the channel record
        tile.SubstreamUrl = channel.RtspSubstreamUrl;
        tile.MainstreamUrl = channel.RtspMainstreamUrl;

        await StartGridStreamAsync(tile);
    }

    private Task StartGridStreamAsync(LiveTileViewModel tile)
    {
        // Grid mode uses substream for low latency and bandwidth
        return StartStreamDirectAsync(tile, tile.SubstreamUrl);
    }

    private Task StartFocusedStreamAsync(LiveTileViewModel tile)
    {
        // Single-tile/zoom mode uses mainstream for full quality
        return StartStreamDirectAsync(tile, tile.MainstreamUrl);
    }

    private async Task StartStreamDirectAsync(LiveTileViewModel tile, string rtspUrl)
    {
        if (string.IsNullOrWhiteSpace(rtspUrl))
        {
            tile.IsConnected = false;
            tile.IsStreamStarted = false;
            tile.SetError("Channel RTSP URL is empty.");
            return;
        }

        try
        {
            // Instant stream switch: stop old, start new immediately
            await streamingOrchestrator.StartChannelAsync(tile.RenderChannelId, rtspUrl, true);
            tile.IsStreamStarted = true;
            tile.ClearError();
        }
        catch (Exception ex)
        {
            tile.IsConnected = false;
            tile.IsStreamStarted = false;
            tile.SetError($"Stream error: {ex.Message}");
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
            await streamingOrchestrator.StopChannelAsync(tile.RenderChannelId);
            tile.IsStreamStarted = false;
            tile.IsConnected = false;
            tile.ClearError();
        }
        catch
        {
            tile.IsStreamStarted = false;
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
        if (_channelIndex.TryGetValue(channelId, out var cachedChannel))
        {
            return cachedChannel;
        }

        var devices = await deviceService.GetAllAsync();
        RebuildChannelIndex(devices);
        return _channelIndex.TryGetValue(channelId, out var channel) ? channel : null;
    }

    private void RebuildChannelIndex(IReadOnlyCollection<Device> devices)
    {
        _channelIndex.Clear();
        foreach (var channel in devices.SelectMany(x => x.Channels))
        {
            _channelIndex[channel.Id] = channel;
        }
    }

    private async void MetricsTimer_OnTick(object? sender, EventArgs e)
    {
        var activeTiles = Tiles.Where(x => x.IsStreamStarted && x.RenderChannelId != Guid.Empty).ToList();
        if (FocusedTile is { IsStreamStarted: true } focused && focused.RenderChannelId != Guid.Empty)
        {
            activeTiles.Add(focused);
        }

        foreach (var tile in activeTiles)
        {
            try
            {
                var metrics = await streamingOrchestrator.GetMetricsAsync(tile.RenderChannelId);
                tile.Fps = metrics.Fps;
                tile.BitrateKbps = metrics.BitrateKbps;
                tile.IsConnected = metrics.Connected;

                if (metrics.Connected && tile.IsErrorVisible)
                {
                    tile.ClearError();
                }
            }
            catch
            {
                tile.IsConnected = false;
            }
        }
    }
}
