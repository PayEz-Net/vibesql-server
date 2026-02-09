using VibeSQL.Core.DTOs;
using VibeSQL.Core.Models;

namespace VibeSQL.Core.DTOs.Converters;

/// <summary>
/// Converts between IndexCreationResult and IndexCreationResultDto
/// </summary>
public static class IndexResultConverter
{
    /// <summary>
    /// Convert internal model to DTO
    /// </summary>
    public static IndexCreationResultDto ToDto(IndexCreationResult model)
    {
        return new IndexCreationResultDto
        {
            Success = model.Success,
            ErrorMessage = model.ErrorMessage,
            PhysicalIndexName = model.PhysicalIndexName,
            VirtualIndexId = model.VirtualIndexId
        };
    }

    /// <summary>
    /// Convert DTO to internal model
    /// </summary>
    public static IndexCreationResult ToModel(IndexCreationResultDto dto)
    {
        return new IndexCreationResult
        {
            Success = dto.Success,
            ErrorMessage = dto.ErrorMessage,
            PhysicalIndexName = dto.PhysicalIndexName,
            VirtualIndexId = dto.VirtualIndexId
        };
    }
}
