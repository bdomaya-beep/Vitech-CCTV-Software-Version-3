using SentinelVMS.Application.DTOs;

namespace SentinelVMS.Application.Abstractions.Authentication;

public interface IAuthenticationService
{
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}
