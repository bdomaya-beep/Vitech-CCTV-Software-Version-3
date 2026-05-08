using SentinelVMS.Application.DTOs;
using SentinelVMS.Domain.Models;

namespace SentinelVMS.Application.Abstractions.Devices;

public interface IDeviceService
{
    Task<IReadOnlyList<Device>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Device> AddAsync(DeviceUpsertRequest request, CancellationToken cancellationToken = default);
    Task<Device> UpdateAsync(Guid id, DeviceUpsertRequest request, CancellationToken cancellationToken = default);
    Task<Device> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> RefreshChannelNamesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CameraChannel> RenameChannelAsync(Guid channelId, string name, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
