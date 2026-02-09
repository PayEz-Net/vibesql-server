using VibeSQL.Core.DTOs;
using VibeSQL.Core.Models;

namespace VibeSQL.Core.DTOs.Converters;

/// <summary>
/// Converts between IndexDefinition (internal model) and IndexDefinitionDto (wire format)
/// </summary>
public static class IndexDefinitionConverter
{
    /// <summary>
    /// Convert DTO to internal model
    /// </summary>
    public static IndexDefinition ToModel(IndexDefinitionDto dto)
    {
        return new IndexDefinition
        {
            IndexName = dto.IndexName ?? string.Empty,
            TableName = dto.TableName,
            Fields = dto.Fields,
            PartialCondition = dto.PartialCondition,
            IsUnique = dto.IsUnique,
            IndexType = dto.IndexType
        };
    }

    /// <summary>
    /// Convert internal model to DTO
    /// </summary>
    public static IndexDefinitionDto ToDto(IndexDefinition model)
    {
        return new IndexDefinitionDto
        {
            IndexName = model.IndexName,
            TableName = model.TableName,
            Fields = model.Fields,
            PartialCondition = model.PartialCondition,
            IsUnique = model.IsUnique,
            IndexType = model.IndexType
        };
    }
}
