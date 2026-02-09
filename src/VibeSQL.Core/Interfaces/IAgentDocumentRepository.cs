namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for agent_documents table operations.
/// Stores documents in VibeDocument JSON with inline content.
/// </summary>
public interface IAgentDocumentRepository
{
    /// <summary>
    /// Get document by ID.
    /// </summary>
    Task<AgentDocumentData?> GetByIdAsync(int clientId, int documentId);

    /// <summary>
    /// Get specific version of a document.
    /// </summary>
    Task<AgentDocumentData?> GetByIdAndVersionAsync(int clientId, int documentId, int version);

    /// <summary>
    /// List documents for an agent with filtering and pagination.
    /// </summary>
    Task<List<AgentDocumentData>> GetByAgentAsync(
        int clientId,
        string agentName,
        string? docType = null,
        string? search = null,
        bool includeDeleted = false,
        int page = 1,
        int pageSize = 20);

    /// <summary>
    /// Count documents for an agent with filtering.
    /// </summary>
    Task<int> CountByAgentAsync(
        int clientId,
        string agentName,
        string? docType = null,
        string? search = null,
        bool includeDeleted = false);

    /// <summary>
    /// Create a new document.
    /// </summary>
    Task<AgentDocumentData> CreateAsync(
        int clientId,
        int userId,
        string agentName,
        string title,
        string content,
        string docType);

    /// <summary>
    /// Create a new version of an existing document.
    /// </summary>
    Task<AgentDocumentData> CreateVersionAsync(
        int clientId,
        int userId,
        int parentDocumentId,
        string? title,
        string? content,
        string? docType);

    /// <summary>
    /// Soft delete a document.
    /// </summary>
    Task<bool> SoftDeleteAsync(int clientId, int documentId, int userId);

    /// <summary>
    /// Get version history for a document (all versions in chain).
    /// </summary>
    Task<List<AgentDocumentData>> GetVersionHistoryAsync(int clientId, int documentId);
}

/// <summary>
/// Internal data model for repository operations.
/// </summary>
public class AgentDocumentData
{
    public int DocumentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ContentMd { get; set; }
    public string? BlobStorageKey { get; set; }
    public string DocType { get; set; } = "draft";
    public int Version { get; set; } = 1;
    public int? ParentDocumentId { get; set; }
    public long ContentSizeBytes { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }
}
