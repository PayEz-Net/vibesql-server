using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Data.EntityConfigurations.Vibe;

public class AccessControlConfigConfiguration : IEntityTypeConfiguration<AccessControlConfig>
{
    public void Configure(EntityTypeBuilder<AccessControlConfig> builder)
    {
        builder.ToTable("access_control_config", "vibe");

        builder.HasKey(e => e.ClientId);

        builder.Property(e => e.ClientId)
            .HasColumnName("client_id");

        builder.Property(e => e.Mode)
            .HasColumnName("mode")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by");
    }
}
