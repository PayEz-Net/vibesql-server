using System;

namespace VibeSQL.Core.DTOs;

public class VibeCollectionSchemaDto
{
    public int CollectionSchemaId { get; set; }
    public int ClientId { get; set; }
    public string Collection { get; set; } = string.Empty;
    public string? JsonSchema { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystem { get; set; }
    public bool IsLocked { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
}
