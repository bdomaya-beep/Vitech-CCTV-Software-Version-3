using Microsoft.EntityFrameworkCore;
using SentinelVMS.Domain.Models;

namespace SentinelVMS.Infrastructure.Data;

public sealed class SentinelDbContext(DbContextOptions<SentinelDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceGroup> DeviceGroups => Set<DeviceGroup>();
    public DbSet<CameraChannel> CameraChannels => Set<CameraChannel>();
    public DbSet<StreamProfile> StreamProfiles => Set<StreamProfile>();
    public DbSet<EventRecord> EventRecords => Set<EventRecord>();
    public DbSet<RecordingMetadata> RecordingMetadata => Set<RecordingMetadata>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SentinelDbContext).Assembly);
    }
}
