using CloudUsage.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudUsage.Api.Data.Configurations;

public sealed class RawUsageEventConfiguration : IEntityTypeConfiguration<RawUsageEvent>
{
    public void Configure(EntityTypeBuilder<RawUsageEvent> builder)
    {
        builder.ToTable(
            "raw_usage_event",
            table => table.HasCheckConstraint(
                "CK_raw_usage_event_properties_json",
                "[properties_json] IS NULL OR ISJSON([properties_json]) = 1"));

        builder.HasKey(usageEvent => usageEvent.RawEventId);

        builder.Property(usageEvent => usageEvent.RawEventId)
            .HasColumnName("raw_event_id")
            .ValueGeneratedOnAdd();

        builder.Property(usageEvent => usageEvent.EventId)
            .HasColumnName("event_id")
            .IsRequired();

        builder.Property(usageEvent => usageEvent.UserExternalId)
            .HasColumnName("user_external_id")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(usageEvent => usageEvent.ProductCode)
            .HasColumnName("product_code")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(usageEvent => usageEvent.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(usageEvent => usageEvent.OccurredAtUtc)
            .HasColumnName("occurred_at_utc")
            .HasPrecision(3)
            .IsRequired();

        builder.Property(usageEvent => usageEvent.ReceivedAtUtc)
            .HasColumnName("received_at_utc")
            .HasPrecision(3)
            .IsRequired();

        builder.Property(usageEvent => usageEvent.PropertiesJson)
            .HasColumnName("properties_json")
            .HasColumnType("nvarchar(max)");

        builder.Property(usageEvent => usageEvent.IngestionStatus)
            .HasColumnName("ingestion_status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(RawEventIngestionStatus.Pending)
            .IsRequired();

        builder.HasIndex(usageEvent => usageEvent.EventId)
            .IsUnique()
            .HasDatabaseName("UX_raw_usage_event_event_id");

        builder.HasIndex(usageEvent => new { usageEvent.IngestionStatus, usageEvent.ReceivedAtUtc })
            .HasDatabaseName("IX_raw_usage_event_status_received_at");
    }
}
