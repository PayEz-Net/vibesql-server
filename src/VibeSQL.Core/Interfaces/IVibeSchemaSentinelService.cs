using VibeSQL.Core.Models;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// VS-SS (Schema Sentinel) Service
/// Provides schema health monitoring, validation, and cleanup.
/// </summary>
public interface IVibeSchemaSentinelService
{
    /// <summary>
    /// Get health status for all schema versions
    /// </summary>
    Task<List<SchemaHealthInfo>> GetSchemaHealthAsync(int? clientId = null);

    /// <summary>
    /// Detect corrupted schemas (excessive table counts or invalid JSON)
    /// </summary>
    Task<List<CorruptedSchemaInfo>> DetectCorruptionAsync(int? clientId = null, int maxTableThreshold = 100);

    /// <summary>
    /// Rollback a corrupted schema to the last known good version
    /// </summary>
    Task<RollbackResult> RollbackToCleanVersionAsync(int schemaId, int adminUserId);

    /// <summary>
    /// Validate a schema JSON without saving
    /// </summary>
    Task<ValidationResult> ValidateSchemaJsonAsync(string jsonSchema);

    /// <summary>
    /// Clean up all corrupted schemas (dry run or actual)
    /// </summary>
    Task<CleanupResult> CleanupCorruptedSchemasAsync(bool dryRun = true, int maxTableThreshold = 100);
}
