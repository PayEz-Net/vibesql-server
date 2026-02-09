namespace VibeSQL.Core.Models;

/// <summary>
/// Represents a single migration transform operation.
/// Used to define how to migrate a field from one schema version to another.
/// </summary>
public class MigrationTransform
{
    /// <summary>
    /// The JSON path to the field (e.g., "price", "user.email")
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Transform type: multiply, divide, map, default, rename, cast
    /// </summary>
    public string Transform { get; set; } = string.Empty;

    /// <summary>
    /// Transform arguments (e.g., [100] for multiply, {"old": "new"} for map)
    /// </summary>
    public object? Args { get; set; }

    /// <summary>
    /// Optional human-readable reason for the migration
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Represents a migration step from one schema version to another.
/// Contains the list of transforms to apply.
/// </summary>
public class MigrationStep
{
    public int FromVersion { get; set; }
    public int ToVersion { get; set; }
    public List<MigrationTransform> Transforms { get; set; } = new();
}

/// <summary>
/// Result of schema compatibility check.
/// </summary>
public class SchemaCompatibility
{
    /// <summary>
    /// Overall compatibility classification
    /// </summary>
    public CompatibilityLevel Level { get; set; }

    /// <summary>
    /// Whether the schema change is compatible
    /// </summary>
    public bool IsCompatible => Level != CompatibilityLevel.Breaking;

    /// <summary>
    /// Whether migrations are defined for this change
    /// </summary>
    public bool HasMigrations { get; set; }

    /// <summary>
    /// List of detected changes
    /// </summary>
    public List<SchemaFieldChange> Changes { get; set; } = new();

    /// <summary>
    /// Number of documents affected by this change
    /// </summary>
    public int AffectedDocumentCount { get; set; }

    /// <summary>
    /// Warnings or recommendations
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Compatibility level classification
/// </summary>
public enum CompatibilityLevel
{
    /// <summary>
    /// Fully compatible - no changes or only additive changes
    /// </summary>
    FullyCompatible,

    /// <summary>
    /// Forward compatible - new schema can read old documents
    /// </summary>
    ForwardCompatible,

    /// <summary>
    /// Backward compatible - old schema can read new documents
    /// </summary>
    BackwardCompatible,

    /// <summary>
    /// Breaking change - requires migration
    /// </summary>
    Breaking
}

/// <summary>
/// Represents a single field change in a schema
/// </summary>
public class SchemaFieldChange
{
    public string FieldPath { get; set; } = string.Empty;
    public SchemaChangeType ChangeType { get; set; }
    public string? OldType { get; set; }
    public string? NewType { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Type of schema change
/// </summary>
public enum SchemaChangeType
{
    Added,
    Removed,
    TypeChanged,
    RequiredChanged,
    DefaultChanged
}

/// <summary>
/// Result of a document migration operation
/// </summary>
public class DocumentMigrationResult
{
    public bool Success { get; set; }
    public int DocumentId { get; set; }
    public int FromVersion { get; set; }
    public int ToVersion { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> TransformsApplied { get; set; } = new();
}

/// <summary>
/// Result of a bulk migration operation
/// </summary>
public class BulkMigrationResult
{
    public bool Success { get; set; }
    public int TotalDocuments { get; set; }
    public int DocumentsMigrated { get; set; }
    public int DocumentsFailed { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Errors { get; set; } = new();
}
