using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for Vibe collection schema operations.
/// Handles schema CRUD, versioning, and sequence management.
/// </summary>
public interface IVibeSchemaRepository
{
    /// <summary>
    /// Get all active schemas for a client
    /// </summary>
    Task<List<VibeCollectionSchema>> GetAllAsync(int clientId);

    /// <summary>
    /// Get the active schema for a collection
    /// </summary>
    Task<VibeCollectionSchema?> GetActiveSchemaAsync(int clientId, string collection);

    /// <summary>
    /// Get schema by ID
    /// </summary>
    Task<VibeCollectionSchema?> GetByIdAsync(int schemaId);

    /// <summary>
    /// Get all versions of a schema (active and inactive)
    /// </summary>
    Task<List<VibeCollectionSchema>> GetVersionsAsync(int clientId, string collection);

    /// <summary>
    /// Create a new schema
    /// </summary>
    Task<VibeCollectionSchema> CreateAsync(VibeCollectionSchema schema);

    /// <summary>
    /// Update an existing schema
    /// </summary>
    Task<bool> UpdateAsync(VibeCollectionSchema schema);

    /// <summary>
    /// Delete a schema (hard delete)
    /// </summary>
    Task<bool> DeleteAsync(int schemaId);

    /// <summary>
    /// Delete all schemas for a collection
    /// </summary>
    Task<int> DeleteByCollectionAsync(int clientId, string collection);

    /// <summary>
    /// Deactivate all versions of a schema (before creating new version)
    /// </summary>
    Task DeactivateVersionsAsync(int clientId, string collection);

    /// <summary>
    /// Check if a collection exists for a client
    /// </summary>
    Task<bool> CollectionExistsAsync(int clientId, string collection);

    /// <summary>
    /// Create a database sequence for auto-increment
    /// </summary>
    Task CreateSequenceAsync(int clientId, string collection, long startValue = 1);

    /// <summary>
    /// Drop a database sequence
    /// </summary>
    Task DropSequenceAsync(int clientId, string collection);

    /// <summary>
    /// Get the sequence name for a collection
    /// </summary>
    string GetSequenceName(int clientId, string collection);

    /// <summary>
    /// Get the max version number for a collection
    /// </summary>
    Task<int> GetMaxVersionAsync(int clientId, string collection);
}
