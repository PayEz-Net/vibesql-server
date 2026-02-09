using System;

namespace VibeSQL.Core.Entities;

/// <summary>
/// Represents a document stored in the Vibe user data service.
/// Documents are scoped by client_id + owner_user_id for tenant isolation.
/// </summary>
public class VibeDocument
{
    public int DocumentId { get; set; }

    /// <summary>
    /// The IDP client identifier (tenant)
    /// </summary>
    public int ClientId { get; set; }

    /// <summary>
    /// The user who owns this document (for query scoping).
    /// Null for admin/system-created documents with no specific owner.
    /// Maps to user_id column in database for backward compatibility.
    /// </summary>
    public int? OwnerUserId { get; set; }

    /// <summary>
    /// Collection name (e.g., "resumes", "profiles", "settings")
    /// </summary>
    public string Collection { get; set; } = string.Empty;

    /// <summary>
    /// Table name within the collection (e.g., "users", "posts")
    /// Required for table-aware data model (Release 2)
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// The document data as JSONB string
    /// </summary>
    public string? Data { get; set; }

    /// <summary>
    /// Optional reference to a schema for validation
    /// </summary>
    public int? CollectionSchemaId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation property
    public virtual VibeCollectionSchema? CollectionSchema { get; set; }
}
