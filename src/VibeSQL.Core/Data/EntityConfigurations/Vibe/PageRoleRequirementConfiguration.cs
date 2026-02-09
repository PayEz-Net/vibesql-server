using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Data.EntityConfigurations.Vibe;

public class PageRoleRequirementConfiguration : IEntityTypeConfiguration<PageRoleRequirement>
{
    public void Configure(EntityTypeBuilder<PageRoleRequirement> builder)
    {
        builder.ToTable("page_role_requirements", "vibe");

        builder.HasKey(e => e.PageRoleRequirementId);

        builder.Property(e => e.PageRoleRequirementId)
            .HasColumnName("page_role_requirement_id");

        builder.Property(e => e.PagePermissionId)
            .HasColumnName("page_permission_id")
            .IsRequired();

        builder.Property(e => e.RoleName)
            .HasColumnName("role_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(e => e.PagePermissionId)
            .HasDatabaseName("ix_page_role_requirements_page");

        builder.HasIndex(e => e.RoleName)
            .HasDatabaseName("ix_page_role_requirements_role");

        builder.HasIndex(e => new { e.PagePermissionId, e.RoleName })
            .IsUnique()
            .HasDatabaseName("uq_page_role_requirements_page_role");
    }
}
