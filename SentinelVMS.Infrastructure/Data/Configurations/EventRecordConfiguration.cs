using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentinelVMS.Domain.Models;

namespace SentinelVMS.Infrastructure.Data.Configurations;

internal sealed class EventRecordConfiguration : IEntityTypeConfiguration<EventRecord>
{
    public void Configure(EntityTypeBuilder<EventRecord> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("TEXT").IsRequired();
        builder.HasIndex(x => x.OccurredUtc);
    }
}
