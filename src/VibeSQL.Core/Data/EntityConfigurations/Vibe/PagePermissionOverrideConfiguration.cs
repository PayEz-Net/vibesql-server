using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Data.EntityConfigurations.Vibe;

public class PagePermissionOverrideConfiguration : IEntityTypeConfiguration<PagePermissionOverride>
{
    public void Configure(EntityTypeBuilder<PagePermissionOverride> builder)
    {
        builder.ToTable("page_permission_overrides", "vibe");

        builder.HasKey(e => e.OverrideId);

        builder.Property(e => e.OverrideId)
            .HasColumnName("override_id");

        builder.Property(e => e.PagePermissionId)
            .HasColumnName("page_permission_id")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.OverrideType)
            .HasColumnName("override_type")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.Reason)
            .HasColumnName("reason");

        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by");

        // Indexes
        builder.HasIndex(e => e.PagePermissionId)
            .HasDatabaseName("ix_page_permission_overrides_page");

        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("ix_page_permission_overrides_user");

        builder.HasIndex(e => new { e.PagePermissionId, e.UserId })
            .IsUnique()
            .HasDatabaseName("uq_page_permission_overrides_page_user");
    }
}
