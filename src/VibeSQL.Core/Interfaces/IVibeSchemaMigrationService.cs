using System.Text.Json;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Models;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for migrating documents between schema versions.
/// Supports lazy migration on read and bulk migration operations.
/// </summary>
public interface IVibeSchemaMigrationService
{
    /// <summary>
    /// Migrate a single document to the target schema version.
    /// Applies all transforms in the migration path.
    /// </summary>
    /// <param name="document">The document to migrate</param>
    /// <param name="targetSchema">The target schema version</param>
    /// <returns>Migrated document with updated data</returns>
    Task<DocumentMigrationResult> MigrateDocumentAsync(
        VibeDocument document,
        VibeCollectionSchema targetSchema);

    /// <summary>
    /// Resolve the migration path from one schema version to another.
    /// Returns the ordered list of migration steps to apply.
    /// </summary>
    /// <param name="fromSchemaId">Source schema ID</param>
    /// <param name="toSchemaId">Target schema ID</param>
    /// <returns>Ordered list of migration steps</returns>
    Task<List<MigrationStep>> GetMigrationPathAsync(
        int fromSchemaId,
        int toSchemaId);

    /// <summary>
    /// Apply a single transform to a JSON document.
    /// </summary>
    /// <param name="data">The JSON document data</param>
    /// <param name="transform">The transform to apply</param>
    /// <returns>Transformed JSON document</returns>
    Task<JsonDocument> ApplyTransformAsync(
        JsonDocument data,
        MigrationTransform transform);

    /// <summary>
    /// Check compatibility between two schemas.
    /// Determines if the schema change is breaking and whether migrations are needed.
    /// </summary>
    /// <param name="clientId">The client ID</param>
    /// <param name="collection">The collection name</param>
    /// <param name="currentSchema">Current active schema</param>
    /// <param name="proposedSchema">Proposed new schema</param>
    /// <returns>Compatibility analysis result</returns>
    Task<SchemaCompatibility> CheckCompatibilityAsync(
        int clientId,
        string collection,
        VibeCollectionSchema currentSchema,
        JsonDocument proposedSchema);

    /// <summary>
    /// Bulk migrate all documents in a collection to the target schema version.
    /// Processes documents in batches for better performance.
    /// </summary>
    /// <param name="clientId">The client ID</param>
    /// <param name="collection">The collection name</param>
    /// <param name="targetVersion">Target schema version</param>
    /// <param name="batchSize">Number of documents to process per batch</param>
    /// <returns>Bulk migration result with statistics</returns>
    Task<BulkMigrationResult> BulkMigrateCollectionAsync(
        int clientId,
        string collection,
        int targetVersion,
        int batchSize = 100);
}
