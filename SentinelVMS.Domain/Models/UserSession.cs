using SentinelVMS.Domain.Enums;

namespace SentinelVMS.Domain.Models;

public sealed class UserSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTimeOffset LoginUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActivityUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool RememberMe { get; set; }
}
