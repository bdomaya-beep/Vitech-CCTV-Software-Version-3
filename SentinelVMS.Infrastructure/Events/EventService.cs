using Microsoft.EntityFrameworkCore;
using SentinelVMS.Application.Abstractions.Events;
using SentinelVMS.Domain.Models;
using SentinelVMS.Infrastructure.Data;

namespace SentinelVMS.Infrastructure.Events;

public sealed class EventService(SentinelDbContext dbContext) : IEventService
{
    public async Task<IReadOnlyList<EventRecord>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        return await dbContext.EventRecords
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task RaiseAsync(EventRecord record, CancellationToken cancellationToken = default)
    {
        await dbContext.EventRecords.AddAsync(record, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
