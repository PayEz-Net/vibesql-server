using VibeSQL.Core.DTOs;
using VibeSQL.Core.Models;

namespace VibeSQL.Core.DTOs.Converters;

/// <summary>
/// Converts between IndexInfo and IndexInfoDto
/// </summary>
public static class IndexInfoConverter
{
    /// <summary>
    /// Convert internal model to DTO
    /// </summary>
    public static IndexInfoDto ToDto(IndexInfo model)
    {
        return new IndexInfoDto
        {
            VirtualIndexId = model.VirtualIndexId,
            IndexName = model.IndexName,
            TableName = model.TableName,
            PhysicalIndexName = model.PhysicalIndexName,
            PartitionName = model.PartitionName,
            Fields = model.Fields,
            CreatedAt = model.CreatedAt
        };
    }

    /// <summary>
    /// Convert DTO to internal model
    /// </summary>
    public static IndexInfo ToModel(IndexInfoDto dto)
    {
        return new IndexInfo
        {
            VirtualIndexId = dto.VirtualIndexId,
            IndexName = dto.IndexName,
            TableName = dto.TableName,
            PhysicalIndexName = dto.PhysicalIndexName,
            PartitionName = dto.PartitionName,
            Fields = dto.Fields,
            CreatedAt = dto.CreatedAt
        };
    }
}
