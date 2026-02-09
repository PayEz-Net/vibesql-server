using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Data.EntityConfigurations.Vibe;

public class VibeDocumentConfiguration : IEntityTypeConfiguration<VibeDocument>
{
    public void Configure(EntityTypeBuilder<VibeDocument> builder)
    {
        builder.ToTable("documents", "vibe");

        // Composite PK required for LIST partitioning by client_id
        // See VIBE-PARTITION-ARCHITECTURE.md for details
        builder.HasKey(d => new { d.DocumentId, d.ClientId })
            .HasName("documents_pkey");

        builder.Property(d => d.DocumentId)
            .HasColumnName("document_id")
            .HasColumnType("integer")
            .ValueGeneratedOnAdd();

        builder.Property(d => d.ClientId)
            .HasColumnName("client_id")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(d => d.OwnerUserId)
            .HasColumnName("user_id")  // Keep existing column name for backward compatibility
            .HasColumnType("integer")
            .IsRequired(false);  // Nullable for admin/system-created documents

        builder.Property(d => d.Collection)
            .HasColumnName("collection")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.TableName)
            .HasColumnName("table_name")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.Data)
            .HasColumnName("data")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(d => d.CollectionSchemaId)
            .HasColumnName("collection_schema_id")
            .HasColumnType("integer");

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(d => d.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("integer");

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(d => d.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("integer");

        builder.Property(d => d.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");

        // Indexes
        builder.HasIndex(d => new { d.ClientId, d.OwnerUserId })
            .HasDatabaseName("idx_documents_tenant");

        builder.HasIndex(d => new { d.ClientId, d.OwnerUserId, d.Collection, d.TableName })
            .HasDatabaseName("idx_documents_collection_table");

        // Foreign key to schema
        builder.HasOne(d => d.CollectionSchema)
            .WithMany()
            .HasForeignKey(d => d.CollectionSchemaId)
            .HasConstraintName("documents_collection_schema_id_fkey")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
