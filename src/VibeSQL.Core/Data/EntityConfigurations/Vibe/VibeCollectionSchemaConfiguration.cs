using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Data.EntityConfigurations.Vibe;

public class VibeCollectionSchemaConfiguration : IEntityTypeConfiguration<VibeCollectionSchema>
{
    public void Configure(EntityTypeBuilder<VibeCollectionSchema> builder)
    {
        builder.ToTable("collection_schemas", "vibe");

        builder.HasKey(s => s.CollectionSchemaId)
            .HasName("collection_schemas_pkey");

        builder.Property(s => s.CollectionSchemaId)
            .HasColumnName("collection_schema_id")
            .HasColumnType("integer")
            .ValueGeneratedOnAdd();

        builder.Property(s => s.ClientId)
            .HasColumnName("client_id")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(s => s.Collection)
            .HasColumnName("collection")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.JsonSchema)
            .HasColumnName("json_schema")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(s => s.Version)
            .HasColumnName("version")
            .HasColumnType("integer")
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(s => s.IsActive)
            .HasColumnName("is_active")
            .HasColumnType("boolean")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(s => s.IsSystem)
            .HasColumnName("is_system")
            .HasColumnType("boolean")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.IsLocked)
            .HasColumnName("is_locked")
            .HasColumnType("boolean")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(s => s.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("integer");

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(s => s.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("integer");

        // Unique constraint
        builder.HasIndex(s => new { s.ClientId, s.Collection, s.Version })
            .HasDatabaseName("collection_schemas_client_id_collection_version_key")
            .IsUnique();
    }
}
