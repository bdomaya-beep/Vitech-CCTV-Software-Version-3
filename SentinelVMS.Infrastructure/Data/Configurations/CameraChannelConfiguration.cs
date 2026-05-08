using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentinelVMS.Domain.Models;

namespace SentinelVMS.Infrastructure.Data.Configurations;

internal sealed class CameraChannelConfiguration : IEntityTypeConfiguration<CameraChannel>
{
    public void Configure(EntityTypeBuilder<CameraChannel> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RtspMainstreamUrl).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.RtspSubstreamUrl).HasMaxLength(1024).IsRequired();
        builder.HasOne(x => x.Device)
            .WithMany(d => d.Channels)
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
