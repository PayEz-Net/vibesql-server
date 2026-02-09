using VibeSQL.Core.Entities;
using VibeSQL.Core.DTOs;

namespace VibeSQL.Core.DTOs.Converters;

/// <summary>
/// Converts between VirtualIndex entity and VirtualIndexDto
/// </summary>
public static class VirtualIndexConverter
{
    /// <summary>
    /// Convert entity to DTO
    /// </summary>
    public static VirtualIndexDto ToDto(VirtualIndex entity)
    {
        return new VirtualIndexDto
        {
            VirtualIndexId = entity.VirtualIndexId,
            ClientId = entity.ClientId,
            Collection = entity.Collection,
            TableName = entity.TableName,
            IndexName = entity.IndexName,
            PhysicalIndexName = entity.PhysicalIndexName,
            IndexDefinition = entity.IndexDefinition,
            PartitionName = entity.PartitionName,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            DroppedAt = entity.DroppedAt
        };
    }

    /// <summary>
    /// Convert DTO to entity
    /// </summary>
    public static VirtualIndex ToEntity(VirtualIndexDto dto)
    {
        return new VirtualIndex
        {
            VirtualIndexId = dto.VirtualIndexId,
            ClientId = dto.ClientId,
            Collection = dto.Collection,
            TableName = dto.TableName,
            IndexName = dto.IndexName,
            PhysicalIndexName = dto.PhysicalIndexName,
            IndexDefinition = dto.IndexDefinition,
            PartitionName = dto.PartitionName,
            CreatedAt = dto.CreatedAt,
            CreatedBy = dto.CreatedBy,
            DroppedAt = dto.DroppedAt
        };
    }
}
