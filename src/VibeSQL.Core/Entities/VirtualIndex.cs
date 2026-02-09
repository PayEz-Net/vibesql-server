using System;

namespace VibeSQL.Core.Entities;

/// <summary>
/// Tracks user-declared virtual indexes on JSONB fields.
/// Physical indexes are created per-partition based on these definitions.
/// </summary>
public class VirtualIndex
{
    public int VirtualIndexId { get; set; }

    /// <summary>
    /// The IDP client identifier (tenant)
    /// </summary>
    public int ClientId { get; set; }

    /// <summary>
    /// Collection name this index applies to
    /// </summary>
    public string Collection { get; set; } = string.Empty;

    /// <summary>
    /// Table name this index applies to
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Logical index name (user-friendly)
    /// </summary>
    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Physical PostgreSQL index name (includes client ID and hash)
    /// </summary>
    public string PhysicalIndexName { get; set; } = string.Empty;

    /// <summary>
    /// Index definition as JSON (fields, partial condition, unique flag)
    /// </summary>
    public string IndexDefinition { get; set; } = string.Empty;

    /// <summary>
    /// Partition where this index is created
    /// </summary>
    public string PartitionName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedBy { get; set; }

    /// <summary>
    /// When the index was dropped (soft delete)
    /// </summary>
    public DateTimeOffset? DroppedAt { get; set; }
}
