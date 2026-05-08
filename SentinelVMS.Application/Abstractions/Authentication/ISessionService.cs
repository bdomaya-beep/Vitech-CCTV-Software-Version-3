using SentinelVMS.Domain.Models;

namespace SentinelVMS.Application.Abstractions.Authentication;

public interface ISessionService
{
    UserSession? CurrentSession { get; }
    Task SetSessionAsync(UserSession session, CancellationToken cancellationToken = default);
    Task ClearSessionAsync(CancellationToken cancellationToken = default);
}
