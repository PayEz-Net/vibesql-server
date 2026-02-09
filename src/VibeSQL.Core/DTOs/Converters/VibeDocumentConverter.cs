using VibeSQL.Core.Entities;
using VibeSQL.Core.DTOs;
using System.Collections.Generic;
using System.Linq;

namespace VibeSQL.Core.DTOs.Converters;

public static class VibeDocumentConverter
{
    public static VibeDocumentDto ToDto(this VibeDocument entity)
    {
        if (entity == null) return null!;
        return new VibeDocumentDto
        {
            DocumentId = entity.DocumentId,
            ClientId = entity.ClientId,
            OwnerUserId = entity.OwnerUserId,
            Collection = entity.Collection,
            TableName = entity.TableName,
            Data = entity.Data,
            CollectionSchemaId = entity.CollectionSchemaId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            DeletedAt = entity.DeletedAt
        };
    }

    public static List<VibeDocumentDto> ToDtos(this IEnumerable<VibeDocument> entities)
    {
        if (entities == null) return null!;
        return entities.Select(e => e.ToDto()).ToList();
    }

    public static VibeDocument ToEntity(this VibeDocumentDto dto)
    {
        if (dto == null) return null!;
        return new VibeDocument
        {
            DocumentId = dto.DocumentId,
            ClientId = dto.ClientId,
            OwnerUserId = dto.OwnerUserId,
            Collection = dto.Collection,
            TableName = dto.TableName,
            Data = dto.Data,
            CollectionSchemaId = dto.CollectionSchemaId,
            CreatedAt = dto.CreatedAt,
            CreatedBy = dto.CreatedBy,
            UpdatedAt = dto.UpdatedAt,
            UpdatedBy = dto.UpdatedBy,
            DeletedAt = dto.DeletedAt
        };
    }

    public static List<VibeDocument> ToEntities(this IEnumerable<VibeDocumentDto> dtos)
    {
        if (dtos == null) return null!;
        return dtos.Select(d => d.ToEntity()).ToList();
    }

    public static void MapToEntity(this VibeDocumentDto dto, VibeDocument entity)
    {
        if (dto == null || entity == null) return;
        entity.ClientId = dto.ClientId;
        entity.OwnerUserId = dto.OwnerUserId;
        entity.Collection = dto.Collection;
        entity.TableName = dto.TableName;
        entity.Data = dto.Data;
        entity.CollectionSchemaId = dto.CollectionSchemaId;
        entity.UpdatedAt = dto.UpdatedAt;
        entity.UpdatedBy = dto.UpdatedBy;
        entity.DeletedAt = dto.DeletedAt;
    }
}
