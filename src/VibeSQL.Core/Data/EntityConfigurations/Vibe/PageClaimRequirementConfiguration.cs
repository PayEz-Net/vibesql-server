using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Data.EntityConfigurations.Vibe;

public class PageClaimRequirementConfiguration : IEntityTypeConfiguration<PageClaimRequirement>
{
    public void Configure(EntityTypeBuilder<PageClaimRequirement> builder)
    {
        builder.ToTable("page_claim_requirements", "vibe");

        builder.HasKey(e => e.PageClaimRequirementId);

        builder.Property(e => e.PageClaimRequirementId)
            .HasColumnName("page_claim_requirement_id");

        builder.Property(e => e.PagePermissionId)
            .HasColumnName("page_permission_id")
            .IsRequired();

        builder.Property(e => e.ClaimType)
            .HasColumnName("claim_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ClaimValue)
            .HasColumnName("claim_value")
            .HasMaxLength(200);

        builder.Property(e => e.IsRequired)
            .HasColumnName("is_required")
            .HasDefaultValue(true);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(e => e.PagePermissionId)
            .HasDatabaseName("ix_page_claim_requirements_page");

        builder.HasIndex(e => e.ClaimType)
            .HasDatabaseName("ix_page_claim_requirements_claim_type");

        builder.HasIndex(e => new { e.PagePermissionId, e.ClaimType, e.ClaimValue })
            .IsUnique()
            .HasDatabaseName("uq_page_claim_requirements_page_claim");
    }
}
