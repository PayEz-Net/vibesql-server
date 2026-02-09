using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Data.EntityConfigurations.Vibe;

public class TierFeatureConfiguration : IEntityTypeConfiguration<TierFeature>
{
    public void Configure(EntityTypeBuilder<TierFeature> builder)
    {
        builder.ToTable("tier_features", "vibe");

        builder.HasKey(f => f.TierFeatureId)
            .HasName("tier_features_pkey");

        builder.Property(f => f.TierFeatureId)
            .HasColumnName("tier_feature_id")
            .HasColumnType("integer")
            .ValueGeneratedOnAdd();

        builder.Property(f => f.TierConfigurationId)
            .HasColumnName("tier_configuration_id")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(f => f.FeatureKey)
            .HasColumnName("feature_key")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(f => f.FeatureName)
            .HasColumnName("feature_name")
            .HasColumnType("varchar(200)")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(f => f.IsEnabled)
            .HasColumnName("is_enabled")
            .HasColumnType("boolean")
            .HasDefaultValue(true);

        builder.Property(f => f.LimitValue)
            .HasColumnName("limit_value")
            .HasColumnType("integer")
            .HasDefaultValue(0);

        builder.Property(f => f.LimitPeriod)
            .HasColumnName("limit_period")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20);

        builder.Property(f => f.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(f => f.SortOrder)
            .HasColumnName("sort_order")
            .HasColumnType("integer")
            .HasDefaultValue(0);

        builder.Property(f => f.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(f => f.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("integer");

        builder.Property(f => f.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(f => f.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("integer");

        // Indexes
        builder.HasIndex(f => f.TierConfigurationId)
            .HasDatabaseName("idx_tier_features_tier_configuration");

        builder.HasIndex(f => new { f.TierConfigurationId, f.FeatureKey })
            .IsUnique()
            .HasDatabaseName("idx_tier_features_tier_feature_key");

        builder.HasIndex(f => f.FeatureKey)
            .HasDatabaseName("idx_tier_features_feature_key");
    }
}
