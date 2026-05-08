using SentinelVMS.Application.Abstractions.Authentication;
using SentinelVMS.Domain.Models;

namespace SentinelVMS.Infrastructure.Authentication;

public sealed class SessionService : ISessionService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public UserSession? CurrentSession { get; private set; }

    public async Task SetSessionAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            CurrentSession = session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearSessionAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            CurrentSession = null;
        }
        finally
        {
            _gate.Release();
        }
    }
}
