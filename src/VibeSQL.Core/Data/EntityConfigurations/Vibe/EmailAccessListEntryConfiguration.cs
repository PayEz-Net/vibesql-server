using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Data.EntityConfigurations.Vibe;

public class EmailAccessListEntryConfiguration : IEntityTypeConfiguration<EmailAccessListEntry>
{
    public void Configure(EntityTypeBuilder<EmailAccessListEntry> builder)
    {
        builder.ToTable("email_access_list", "vibe");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.ClientId)
            .HasColumnName("client_id")
            .IsRequired();

        builder.Property(e => e.ListType)
            .HasColumnName("list_type")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.Reason)
            .HasColumnName("reason")
            .HasMaxLength(500);

        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by");

        // Indexes
        builder.HasIndex(e => e.ClientId)
            .HasDatabaseName("idx_email_access_list_client_id");

        builder.HasIndex(e => e.Email)
            .HasDatabaseName("idx_email_access_list_email");
    }
}
