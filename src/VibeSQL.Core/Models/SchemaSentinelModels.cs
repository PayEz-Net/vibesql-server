namespace VibeSQL.Core.Models;

/// <summary>
/// Schema health status levels
/// </summary>
public enum SchemaHealthStatus
{
    Healthy,
    Warning,
    Invalid,
    Critical
}

/// <summary>
/// Schema health information returned by the Schema Sentinel
/// </summary>
public class SchemaHealthInfo
{
    public int CollectionSchemaId { get; set; }
    public int ClientId { get; set; }
    public string Collection { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
    public bool IsSystem { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int TableCount { get; set; }
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public int JsonSizeBytes { get; set; }
    public SchemaHealthStatus HealthStatus { get; set; }
}

/// <summary>
/// Corrupted schema information
/// </summary>
public class CorruptedSchemaInfo
{
    public int CollectionSchemaId { get; set; }
    public int ClientId { get; set; }
    public string Collection { get; set; } = string.Empty;
    public int Version { get; set; }
    public int TableCount { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasCleanVersionAvailable { get; set; }
    public int? CleanVersionId { get; set; }
}

/// <summary>
/// Schema rollback result
/// </summary>
public class RollbackResult
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int? NewVersion { get; set; }
    public int? RolledBackFromVersion { get; set; }
    public int TableCountAfter { get; set; }
}

/// <summary>
/// Schema validation result
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public int TableCount { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> TableNames { get; set; } = new();
}

/// <summary>
/// Cleanup operation result
/// </summary>
public class CleanupResult
{
    public bool Success { get; set; }
    public int TotalCorrupted { get; set; }
    public int RolledBack { get; set; }
    public int Flagged { get; set; }
    public List<CleanupAction> Actions { get; set; } = new();
}

/// <summary>
/// Individual cleanup action
/// </summary>
public class CleanupAction
{
    public string Action { get; set; } = string.Empty;
    public int CollectionSchemaId { get; set; }
    public int ClientId { get; set; }
    public string Collection { get; set; } = string.Empty;
    public int Version { get; set; }
    public int TableCount { get; set; }
    public string? Details { get; set; }
}
