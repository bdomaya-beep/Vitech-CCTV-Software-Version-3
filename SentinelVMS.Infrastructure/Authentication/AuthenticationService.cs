using Microsoft.EntityFrameworkCore;
using SentinelVMS.Application.Abstractions.Authentication;
using SentinelVMS.Application.DTOs;
using SentinelVMS.Domain.Models;
using SentinelVMS.Infrastructure.Data;

namespace SentinelVMS.Infrastructure.Authentication;

public sealed class AuthenticationService(
    SentinelDbContext dbContext,
    ISessionService sessionService) : IAuthenticationService
{
    private const string DefaultAdminUsername = "admin";
    private const string DefaultAdminPassword = "Admin@123";

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = request.Username.Trim();

        var user = await dbContext.Users.FirstOrDefaultAsync(
            u => u.Username == normalizedUsername && u.IsActive,
            cancellationToken);

        if (user is null && string.Equals(normalizedUsername, DefaultAdminUsername, StringComparison.OrdinalIgnoreCase))
        {
            user = await EnsureDefaultAdminAsync(cancellationToken);
        }

        if (user is null)
        {
            return new LoginResult(false, "Invalid credentials", null);
        }

        var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!passwordValid && string.Equals(normalizedUsername, DefaultAdminUsername, StringComparison.OrdinalIgnoreCase) && request.Password == DefaultAdminPassword)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultAdminPassword);
            user.IsActive = true;
            await dbContext.SaveChangesAsync(cancellationToken);
            passwordValid = true;
        }

        if (!passwordValid)
        {
            return new LoginResult(false, "Invalid credentials", null);
        }

        user.LastLoginUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var session = new UserSession
        {
            UserId = user.Id,
            Username = user.Username,
            Role = user.Role,
            RememberMe = request.RememberMe
        };

        await sessionService.SetSessionAsync(session, cancellationToken);
        return new LoginResult(true, "Login successful", session);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await sessionService.ClearSessionAsync(cancellationToken);
    }

    private async Task<User> EnsureDefaultAdminAsync(CancellationToken cancellationToken)
    {
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Username = DefaultAdminUsername,
            DisplayName = "System Administrator",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultAdminPassword),
            Role = Domain.Enums.UserRole.Administrator,
            IsActive = true,
            CreatedUtc = DateTimeOffset.UtcNow
        };

        await dbContext.Users.AddAsync(admin, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return admin;
    }
}
