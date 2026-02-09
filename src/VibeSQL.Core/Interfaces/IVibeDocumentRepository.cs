using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for Vibe document operations.
/// Handles CRUD and complex JSONB queries for the vibe.documents table.
/// </summary>
public interface IVibeDocumentRepository
{
    /// <summary>
    /// Get a document by ID (no userId filter for admin/explorer use)
    /// </summary>
    Task<VibeDocument?> GetByIdAsync(int clientId, string collection, string tableName, int documentId);

    /// <summary>
    /// List documents with pagination
    /// </summary>
    Task<(List<VibeDocument> Documents, int TotalCount)> ListAsync(
        int clientId, string collection, string tableName, int page, int pageSize);

    /// <summary>
    /// Create a document and return it with generated keys
    /// </summary>
    Task<(VibeDocument Document, Dictionary<string, object>? GeneratedKeys)> CreateAsync(
        int clientId, int userId, string collection, string tableName, string data, int? createdBy);

    /// <summary>
    /// Update a document's data
    /// </summary>
    Task<VibeDocument?> UpdateAsync(int documentId, string data, int? updatedBy);

    /// <summary>
    /// Patch (partial update) a document's data
    /// </summary>
    Task<VibeDocument?> PatchAsync(int documentId, Dictionary<string, object?> patchData, int? updatedBy);

    /// <summary>
    /// Soft delete a document
    /// </summary>
    Task<bool> DeleteAsync(int documentId);

    /// <summary>
    /// Get the next sequence value for auto-increment fields
    /// </summary>
    Task<long> GetNextSequenceValueAsync(string sequenceName);

    /// <summary>
    /// Get document count by collection/table
    /// </summary>
    Task<int> GetDocumentCountAsync(int clientId, string collection, string? tableName = null);

    /// <summary>
    /// Find documents by JSONB field value
    /// </summary>
    Task<List<VibeDocument>> FindByFieldAsync(int clientId, string collection, string tableName, string fieldName, object value);

    /// <summary>
    /// Track encrypted value ownership for cross-tenant validation
    /// </summary>
    Task TrackEncryptedValueOwnershipAsync(string ciphertextHash, int clientId, int keyId);
}
