using SentinelVMS.Domain.Models;

namespace SentinelVMS.Application.Abstractions.Events;

public interface IEventService
{
    Task<IReadOnlyList<EventRecord>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
    Task RaiseAsync(EventRecord record, CancellationToken cancellationToken = default);
}
