using Microsoft.EntityFrameworkCore;
using SentinelVMS.Domain.Enums;
using SentinelVMS.Domain.Models;

namespace SentinelVMS.Infrastructure.Data;

public sealed class DatabaseInitializer(SentinelDbContext dbContext)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Create database and schema if it doesn't exist
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        // Seed default admin user if no users exist
        if (!await dbContext.Users.AnyAsync(cancellationToken))
        {
            dbContext.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                DisplayName = "System Administrator",
                Role = UserRole.Administrator,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                IsActive = true,
                CreatedUtc = DateTimeOffset.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
