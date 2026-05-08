using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentinelVMS.Domain.Models;

namespace SentinelVMS.Infrastructure.Data.Configurations;

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Host).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Port).IsRequired();
        builder.HasOne(x => x.Group)
            .WithMany(g => g.Devices)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
