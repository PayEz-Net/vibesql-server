using VibeSQL.Core.Entities;
using VibeSQL.Core.DTOs;
using System.Collections.Generic;
using System.Linq;

namespace VibeSQL.Core.DTOs.Converters;

public static class VibeCollectionSchemaConverter
{
    public static VibeCollectionSchemaDto ToDto(this VibeCollectionSchema entity)
    {
        if (entity == null) return null!;
        return new VibeCollectionSchemaDto
        {
            CollectionSchemaId = entity.CollectionSchemaId,
            ClientId = entity.ClientId,
            Collection = entity.Collection,
            JsonSchema = entity.JsonSchema,
            Version = entity.Version,
            IsActive = entity.IsActive,
            IsSystem = entity.IsSystem,
            IsLocked = entity.IsLocked,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static List<VibeCollectionSchemaDto> ToDtos(this IEnumerable<VibeCollectionSchema> entities)
    {
        if (entities == null) return null!;
        return entities.Select(e => e.ToDto()).ToList();
    }

    public static VibeCollectionSchema ToEntity(this VibeCollectionSchemaDto dto)
    {
        if (dto == null) return null!;
        return new VibeCollectionSchema
        {
            CollectionSchemaId = dto.CollectionSchemaId,
            ClientId = dto.ClientId,
            Collection = dto.Collection,
            JsonSchema = dto.JsonSchema,
            Version = dto.Version,
            IsActive = dto.IsActive,
            IsSystem = dto.IsSystem,
            IsLocked = dto.IsLocked,
            CreatedAt = dto.CreatedAt,
            CreatedBy = dto.CreatedBy,
            UpdatedAt = dto.UpdatedAt,
            UpdatedBy = dto.UpdatedBy
        };
    }

    public static List<VibeCollectionSchema> ToEntities(this IEnumerable<VibeCollectionSchemaDto> dtos)
    {
        if (dtos == null) return null!;
        return dtos.Select(d => d.ToEntity()).ToList();
    }

    public static void MapToEntity(this VibeCollectionSchemaDto dto, VibeCollectionSchema entity)
    {
        if (dto == null || entity == null) return;
        entity.ClientId = dto.ClientId;
        entity.Collection = dto.Collection;
        entity.JsonSchema = dto.JsonSchema;
        entity.Version = dto.Version;
        entity.IsActive = dto.IsActive;
        entity.IsSystem = dto.IsSystem;
        entity.IsLocked = dto.IsLocked;
        entity.UpdatedAt = dto.UpdatedAt;
        entity.UpdatedBy = dto.UpdatedBy;
    }
}
