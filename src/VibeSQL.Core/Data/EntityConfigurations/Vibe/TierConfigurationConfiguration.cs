using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Data.EntityConfigurations.Vibe;

public class TierConfigurationConfiguration : IEntityTypeConfiguration<TierConfiguration>
{
    public void Configure(EntityTypeBuilder<TierConfiguration> builder)
    {
        builder.ToTable("tier_configurations", "vibe");

        builder.HasKey(t => t.TierConfigurationId)
            .HasName("tier_configurations_pkey");

        builder.Property(t => t.TierConfigurationId)
            .HasColumnName("tier_configuration_id")
            .HasColumnType("integer")
            .ValueGeneratedOnAdd();

        builder.Property(t => t.ClientId)
            .HasColumnName("client_id")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(t => t.TierKey)
            .HasColumnName("tier_key")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.DisplayName)
            .HasColumnName("display_name")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(t => t.SortOrder)
            .HasColumnName("sort_order")
            .HasColumnType("integer")
            .HasDefaultValue(0);

        builder.Property(t => t.IsDefault)
            .HasColumnName("is_default")
            .HasColumnType("boolean")
            .HasDefaultValue(false);

        builder.Property(t => t.IsActive)
            .HasColumnName("is_active")
            .HasColumnType("boolean")
            .HasDefaultValue(true);

        builder.Property(t => t.MonthlyPriceCents)
            .HasColumnName("monthly_price_cents")
            .HasColumnType("integer")
            .HasDefaultValue(0);

        builder.Property(t => t.StripePriceId)
            .HasColumnName("stripe_price_id")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100);

        builder.Property(t => t.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(t => t.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("integer");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(t => t.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("integer");

        // Indexes
        builder.HasIndex(t => t.ClientId)
            .HasDatabaseName("idx_tier_configurations_client");

        builder.HasIndex(t => new { t.ClientId, t.TierKey })
            .IsUnique()
            .HasDatabaseName("idx_tier_configurations_client_tier_key");

        // Navigation to features
        builder.HasMany(t => t.Features)
            .WithOne(f => f.TierConfiguration)
            .HasForeignKey(f => f.TierConfigurationId)
            .HasConstraintName("tier_features_tier_configuration_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
