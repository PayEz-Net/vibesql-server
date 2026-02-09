using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Data.EntityConfigurations.Vibe;

public class VibeEncryptedValueOwnershipConfiguration : IEntityTypeConfiguration<VibeEncryptedValueOwnership>
{
    public void Configure(EntityTypeBuilder<VibeEncryptedValueOwnership> builder)
    {
        builder.ToTable("encrypted_value_ownership", "vibe");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.CiphertextHash)
            .HasColumnName("ciphertext_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.ClientId)
            .HasColumnName("client_id")
            .IsRequired();

        builder.Property(e => e.KeyId)
            .HasColumnName("key_id")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(e => e.CiphertextHash)
            .HasDatabaseName("ix_encrypted_value_ownership_hash");

        builder.HasIndex(e => e.ClientId)
            .HasDatabaseName("ix_encrypted_value_ownership_client");
    }
}
