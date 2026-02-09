using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Data.EntityConfigurations.Vibe;

public class PagePermissionConfiguration : IEntityTypeConfiguration<PagePermission>
{
    public void Configure(EntityTypeBuilder<PagePermission> builder)
    {
        builder.ToTable("page_permissions", "vibe");

        builder.HasKey(e => e.PagePermissionId);

        builder.Property(e => e.PagePermissionId)
            .HasColumnName("page_permission_id");

        builder.Property(e => e.ClientId)
            .HasColumnName("client_id")
            .IsRequired();

        builder.Property(e => e.RoutePattern)
            .HasColumnName("route_pattern")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description");

        builder.Property(e => e.RoleLogic)
            .HasColumnName("role_logic")
            .HasMaxLength(10)
            .HasDefaultValue("any");

        builder.Property(e => e.Requires2fa)
            .HasColumnName("requires_2fa")
            .HasDefaultValue(false);

        builder.Property(e => e.RequiresElevatedAuth)
            .HasColumnName("requires_elevated_auth")
            .HasDefaultValue(false);

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(e => e.SortOrder)
            .HasColumnName("sort_order")
            .HasDefaultValue(0);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by");

        // Indexes
        builder.HasIndex(e => e.ClientId)
            .HasDatabaseName("ix_page_permissions_client");

        builder.HasIndex(e => e.RoutePattern)
            .HasDatabaseName("ix_page_permissions_route");

        builder.HasIndex(e => new { e.ClientId, e.RoutePattern })
            .IsUnique()
            .HasDatabaseName("uq_page_permissions_client_route");

        // Relationships
        builder.HasMany(e => e.RoleRequirements)
            .WithOne(r => r.PagePermission)
            .HasForeignKey(r => r.PagePermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Overrides)
            .WithOne(o => o.PagePermission)
            .HasForeignKey(o => o.PagePermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.ClaimRequirements)
            .WithOne(c => c.PagePermission)
            .HasForeignKey(c => c.PagePermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
