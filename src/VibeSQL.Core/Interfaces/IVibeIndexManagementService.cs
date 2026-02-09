using System.Text.Json;
using VibeSQL.Core.Models;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for managing virtual indexes on JSONB fields in Vibe documents.
/// </summary>
public interface IVibeIndexManagementService
{
    /// <summary>
    /// Synchronize indexes for a schema - create new indexes and drop orphaned ones.
    /// Called after schema create/update operations.
    /// </summary>
    Task<List<IndexCreationResult>> SyncIndexesForSchemaAsync(
        int clientId,
        string collection,
        JsonDocument jsonSchema);

    /// <summary>
    /// Create a virtual index on a specific field.
    /// </summary>
    Task<IndexCreationResult> CreateVirtualIndexAsync(
        int clientId,
        string collection,
        string tableName,
        IndexDefinition indexDef);

    /// <summary>
    /// Drop a virtual index by name.
    /// </summary>
    Task<bool> DropVirtualIndexAsync(
        int clientId,
        string collection,
        string indexName);

    /// <summary>
    /// List all indexes for a client's collection.
    /// </summary>
    Task<List<IndexInfo>> ListIndexesForClientAsync(
        int clientId,
        string collection);

    /// <summary>
    /// Get index count for tier limit enforcement.
    /// </summary>
    Task<int> GetIndexCountAsync(int clientId);
}
