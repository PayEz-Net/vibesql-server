using VibeSQL.Core.DTOs.AgentDocs;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Application service for agent document operations.
/// Handles business logic, validation, and versioning.
/// </summary>
public interface IAgentDocumentService
{
    /// <summary>
    /// Upload a new document.
    /// </summary>
    Task<AgentDocUploadResult> UploadAsync(string clientId, int userId, CreateAgentDocRequest request);

    /// <summary>
    /// List documents for an agent with filtering and pagination.
    /// </summary>
    Task<AgentDocListResult> ListAsync(string clientId, string agentName, AgentDocListOptions options);

    /// <summary>
    /// Get a document by ID, optionally a specific version.
    /// </summary>
    Task<AgentDocGetResult> GetAsync(string clientId, int documentId, int? version = null);

    /// <summary>
    /// Update a document (creates a new version).
    /// </summary>
    Task<AgentDocUploadResult> UpdateAsync(string clientId, int userId, int documentId, UpdateAgentDocRequest request);

    /// <summary>
    /// Delete a document (soft delete).
    /// </summary>
    Task<AgentDocDeleteResult> DeleteAsync(string clientId, int userId, int documentId);

    /// <summary>
    /// Get version history for a document.
    /// </summary>
    Task<AgentDocHistoryResult> GetHistoryAsync(string clientId, int documentId);
}
