using Microsoft.EntityFrameworkCore;
using SentinelVMS.Application.Abstractions.Devices;
using SentinelVMS.Application.DTOs;
using SentinelVMS.Domain.Enums;
using SentinelVMS.Domain.Models;
using SentinelVMS.Infrastructure.Data;
using SentinelVMS.Networking.Nvr;
using SentinelVMS.Networking.Tcp;

namespace SentinelVMS.Infrastructure.Devices;

public sealed class DeviceService(
    SentinelDbContext dbContext,
    ITcpProbeService tcpProbeService) : IDeviceService
{
    public async Task<IReadOnlyList<Device>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Devices
            .Include(x => x.Channels)
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Device> AddAsync(DeviceUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Device
        {
            Name = request.Name,
            Host = request.Host,
            Port = request.IsNvr ? 37777 : request.Port,
            Username = request.Username,
            Password = request.Password,
            Manufacturer = request.Manufacturer,
            Model = request.Model
        };

        if (request.IsNvr)
        {
            // Auto-detect channel count and names from the NVR
            var nvrClient = new DahuaNvrClient();
            var autoNames = await nvrClient.AutoDetectChannelNamesAsync(
                entity.Host, entity.Username, entity.Password, cancellationToken);

            // Default to 32 channels for NVRs if auto-detect fails (standard Dahua/Hikvision config)
            var channelCount = autoNames?.Length > 0 ? autoNames.Length : 32;
            
            // Set model to Dahua if not already set and we're connecting as NVR
            if (string.IsNullOrWhiteSpace(entity.Manufacturer))
            {
                entity.Manufacturer = "Dahua";
            }
            if (string.IsNullOrWhiteSpace(entity.Model))
            {
                entity.Model = await DetectNvrModelAsync(entity.Host, entity.Username, entity.Password, cancellationToken) ?? "NVR";
            }

            for (var i = 1; i <= channelCount; i++)
            {
                var realName = autoNames?.ElementAtOrDefault(i - 1);
                var channelName = !string.IsNullOrWhiteSpace(realName)
                    ? realName
                    : $"{entity.Name} CH{i}";

                entity.Channels.Add(new CameraChannel
                {
                    ChannelNumber = i,
                    Name = channelName,
                    RtspMainstreamUrl = BuildNvrRtspUrl(entity, i, false),
                    RtspSubstreamUrl = BuildNvrRtspUrl(entity, i, true),
                    IsEnabled = true
                });
            }
        }

        await dbContext.Devices.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<Device> UpdateAsync(Guid id, DeviceUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Devices.FirstAsync(x => x.Id == id, cancellationToken);
        entity.Name = request.Name;
        entity.Host = request.Host;
        entity.Port = request.Port;
        entity.Username = request.Username;
        entity.Password = request.Password;
        entity.Manufacturer = request.Manufacturer;
        entity.Model = request.Model;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    /// <summary>
    /// Attempts to refresh channel names for an NVR from the real device (Dahua HTTP API).
    /// Returns true if any names were updated.
    /// </summary>
    public async Task<bool> RefreshChannelNamesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Devices
            .Include(x => x.Channels)
            .FirstAsync(x => x.Id == id, cancellationToken);

        var nvrClient = new DahuaNvrClient();
        var realNames = await nvrClient.GetChannelNamesAsync(
            entity.Host, entity.Username, entity.Password, entity.Channels.Count, cancellationToken);

        if (realNames is null) return false;

        var updated = false;
        foreach (var channel in entity.Channels)
        {
            var realName = realNames.ElementAtOrDefault(channel.ChannelNumber - 1);
            if (!string.IsNullOrWhiteSpace(realName) && channel.Name != realName)
            {
                channel.Name = realName;
                updated = true;
            }
        }

        if (updated)
            await dbContext.SaveChangesAsync(cancellationToken);

        return updated;
    }

    public async Task<CameraChannel> RenameChannelAsync(Guid channelId, string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Channel name is required.", nameof(name));
        }

        var channel = await dbContext.CameraChannels
            .FirstAsync(x => x.Id == channelId, cancellationToken);

        channel.Name = normalizedName;
        await dbContext.SaveChangesAsync(cancellationToken);
        return channel;
    }

    public async Task<Device> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Devices
            .Include(x => x.Channels)
            .FirstAsync(x => x.Id == id, cancellationToken);

        var managementReachable = await tcpProbeService.IsOpenAsync(entity.Host, entity.Port, 1500, cancellationToken);
        var rtspReachable = await tcpProbeService.IsOpenAsync(entity.Host, 554, 1500, cancellationToken);

        entity.HealthStatus = managementReachable || rtspReachable
            ? DeviceHealthStatus.Online
            : DeviceHealthStatus.Offline;
        entity.LastSeenUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Devices.FirstAsync(x => x.Id == id, cancellationToken);
        dbContext.Devices.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string BuildNvrRtspUrl(Device device, int channel, bool substream)
    {
        var username = Uri.EscapeDataString(device.Username);
        var password = Uri.EscapeDataString(device.Password);
        var subtype = substream ? 1 : 0;
        return $"rtsp://{username}:{password}@{device.Host}:554/cam/realmonitor?channel={channel}&subtype={subtype}";
    }

    private async Task<string?> DetectNvrModelAsync(string host, string username, string password, CancellationToken cancellationToken)
    {
        try
        {
            // Try to detect model by probing common NVR endpoints
            // Dahua typically responds on port 37777 (TCP management)
            var managementReachable = await tcpProbeService.IsOpenAsync(host, 37777, 1500, cancellationToken);
            if (managementReachable)
            {
                return "Dahua";
            }

            // Try Hikvision ports
            var hikReachable = await tcpProbeService.IsOpenAsync(host, 8000, 1500, cancellationToken);
            if (hikReachable)
            {
                return "Hikvision";
            }

            // Default to generic NVR if RTSP is reachable
            var rtspReachable = await tcpProbeService.IsOpenAsync(host, 554, 1500, cancellationToken);
            return rtspReachable ? "NVR" : null;
        }
        catch
        {
            return null;
        }
    }
}
