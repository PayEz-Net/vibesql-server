using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Data.EntityConfigurations.Vibe;

public class FeatureUsageLogConfiguration : IEntityTypeConfiguration<FeatureUsageLog>
{
    public void Configure(EntityTypeBuilder<FeatureUsageLog> builder)
    {
        builder.ToTable("feature_usage_logs", "vibe");

        builder.HasKey(f => f.FeatureUsageLogId)
            .HasName("feature_usage_logs_pkey");

        builder.Property(f => f.FeatureUsageLogId)
            .HasColumnName("feature_usage_log_id")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();

        builder.Property(f => f.ClientId)
            .HasColumnName("client_id")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(f => f.UserId)
            .HasColumnName("user_id")
            .HasColumnType("integer");

        builder.Property(f => f.FeatureKey)
            .HasColumnName("feature_key")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(f => f.PeriodType)
            .HasColumnName("period_type")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue("monthly");

        builder.Property(f => f.PeriodStart)
            .HasColumnName("period_start")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(f => f.PeriodEnd)
            .HasColumnName("period_end")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(f => f.UsageCount)
            .HasColumnName("usage_count")
            .HasColumnType("bigint")
            .HasDefaultValue(0);

        builder.Property(f => f.PeriodLimit)
            .HasColumnName("period_limit")
            .HasColumnType("integer")
            .HasDefaultValue(-1);

        builder.Property(f => f.LimitExceeded)
            .HasColumnName("limit_exceeded")
            .HasColumnType("boolean")
            .HasDefaultValue(false);

        builder.Property(f => f.FirstUsageAt)
            .HasColumnName("first_usage_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(f => f.LastUsageAt)
            .HasColumnName("last_usage_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(f => f.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(f => f.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        // Indexes for efficient querying
        // Primary lookup: find usage for a client/user/feature in a period
        builder.HasIndex(f => new { f.ClientId, f.UserId, f.FeatureKey, f.PeriodStart })
            .IsUnique()
            .HasDatabaseName("idx_feature_usage_logs_client_user_feature_period");

        // Client-level queries (all features for a client)
        builder.HasIndex(f => new { f.ClientId, f.PeriodStart })
            .HasDatabaseName("idx_feature_usage_logs_client_period");

        // Feature-level queries (usage across all clients)
        builder.HasIndex(f => new { f.FeatureKey, f.PeriodStart })
            .HasDatabaseName("idx_feature_usage_logs_feature_period");

        // Exceeded limits (for alerting/reporting)
        builder.HasIndex(f => f.LimitExceeded)
            .HasFilter("limit_exceeded = true")
            .HasDatabaseName("idx_feature_usage_logs_exceeded");
    }
}
