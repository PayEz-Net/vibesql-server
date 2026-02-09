using System;

namespace VibeSQL.Core.Entities;

/// <summary>
/// Represents a JSON schema for validating documents in a collection.
/// Schemas are versioned and scoped by client_id + collection.
/// </summary>
public class VibeCollectionSchema
{
    public int CollectionSchemaId { get; set; }

    /// <summary>
    /// The IDP client identifier (tenant)
    /// </summary>
    public int ClientId { get; set; }

    /// <summary>
    /// Collection name this schema applies to
    /// </summary>
    public string Collection { get; set; } = string.Empty;

    /// <summary>
    /// The JSON Schema definition as string
    /// </summary>
    public string? JsonSchema { get; set; }

    /// <summary>
    /// Schema version (incremented on updates)
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Whether this is the active schema version for the collection
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// System collections are exempt from tier limits (billing).
    /// </summary>
    public bool IsSystem { get; set; } = false;

    /// <summary>
    /// Locked schemas cannot have structural modifications (add/rename/delete fields).
    /// Data operations (INSERT/UPDATE/DELETE documents) are still allowed.
    /// </summary>
    public bool IsLocked { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
}
