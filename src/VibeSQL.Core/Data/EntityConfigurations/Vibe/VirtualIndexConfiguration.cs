using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Data.EntityConfigurations.Vibe;

public class VirtualIndexConfiguration : IEntityTypeConfiguration<VirtualIndex>
{
    public void Configure(EntityTypeBuilder<VirtualIndex> builder)
    {
        builder.ToTable("virtual_indexes", "vibe");

        builder.HasKey(v => v.VirtualIndexId)
            .HasName("virtual_indexes_pkey");

        builder.Property(v => v.VirtualIndexId)
            .HasColumnName("virtual_index_id")
            .HasColumnType("integer")
            .ValueGeneratedOnAdd();

        builder.Property(v => v.ClientId)
            .HasColumnName("client_id")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(v => v.Collection)
            .HasColumnName("collection")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(v => v.TableName)
            .HasColumnName("table_name")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(v => v.IndexName)
            .HasColumnName("index_name")
            .HasColumnType("varchar(200)")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(v => v.PhysicalIndexName)
            .HasColumnName("physical_index_name")
            .HasColumnType("varchar(200)")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(v => v.IndexDefinition)
            .HasColumnName("index_definition")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(v => v.PartitionName)
            .HasColumnName("partition_name")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(v => v.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(v => v.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("integer");

        builder.Property(v => v.DroppedAt)
            .HasColumnName("dropped_at")
            .HasColumnType("timestamp with time zone");

        // Unique constraint on (client_id, collection, index_name)
        builder.HasIndex(v => new { v.ClientId, v.Collection, v.IndexName })
            .HasDatabaseName("idx_virtual_indexes_client_collection_name")
            .IsUnique();

        // Index on partition_name for active indexes
        builder.HasIndex(v => v.PartitionName)
            .HasDatabaseName("idx_virtual_indexes_partition")
            .HasFilter("dropped_at IS NULL");

        // Index on (client_id, collection) for listing
        builder.HasIndex(v => new { v.ClientId, v.Collection })
            .HasDatabaseName("idx_virtual_indexes_client_collection");
    }
}
