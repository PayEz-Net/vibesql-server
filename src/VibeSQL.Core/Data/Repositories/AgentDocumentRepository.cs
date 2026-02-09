using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for agent_documents table operations.
/// Stores documents in VibeDocument JSON with inline content (max 100KB).
/// </summary>
public class AgentDocumentRepository : IAgentDocumentRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<AgentDocumentRepository> _logger;

    private const string CollectionName = "vibe_agents";
    private const string TableName = "agent_documents";

    public AgentDocumentRepository(VibeDbContext context, ILogger<AgentDocumentRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AgentDocumentData?> GetByIdAsync(int clientId, int documentId)
    {
        var documents = await GetAllDocumentsAsync(clientId);
        return documents.FirstOrDefault(d => d.DocumentId == documentId && !d.IsDeleted);
    }

    public async Task<AgentDocumentData?> GetByIdAndVersionAsync(int clientId, int documentId, int version)
    {
        var documents = await GetAllDocumentsAsync(clientId);
        
        // Find the document and traverse version chain if needed
        var doc = documents.FirstOrDefault(d => d.DocumentId == documentId);
        if (doc == null) return null;

        // If requested version matches, return it
        if (doc.Version == version) return doc;

        // Otherwise, find the specific version in the chain
        var allVersions = await GetVersionHistoryAsync(clientId, documentId);
        return allVersions.FirstOrDefault(v => v.Version == version);
    }

    public async Task<List<AgentDocumentData>> GetByAgentAsync(
        int clientId,
        string agentName,
        string? docType = null,
        string? search = null,
        bool includeDeleted = false,
        int page = 1,
        int pageSize = 20)
    {
        var documents = await GetAllDocumentsAsync(clientId);

        var query = documents
            .Where(d => d.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase));

        if (!includeDeleted)
            query = query.Where(d => !d.IsDeleted);

        if (!string.IsNullOrEmpty(docType))
            query = query.Where(d => d.DocType.Equals(docType, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(search))
            query = query.Where(d => d.Title.Contains(search, StringComparison.OrdinalIgnoreCase));

        // Only return latest versions (documents with no children pointing to them as parent)
        var allDocIds = documents.Select(d => d.DocumentId).ToHashSet();
        var parentIds = documents
            .Where(d => d.ParentDocumentId.HasValue)
            .Select(d => d.ParentDocumentId!.Value)
            .ToHashSet();

        query = query.Where(d => !parentIds.Contains(d.DocumentId));

        return query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    public async Task<int> CountByAgentAsync(
        int clientId,
        string agentName,
        string? docType = null,
        string? search = null,
        bool includeDeleted = false)
    {
        var documents = await GetAllDocumentsAsync(clientId);

        var query = documents
            .Where(d => d.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase));

        if (!includeDeleted)
            query = query.Where(d => !d.IsDeleted);

        if (!string.IsNullOrEmpty(docType))
            query = query.Where(d => d.DocType.Equals(docType, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(search))
            query = query.Where(d => d.Title.Contains(search, StringComparison.OrdinalIgnoreCase));

        // Only count latest versions
        var parentIds = documents
            .Where(d => d.ParentDocumentId.HasValue)
            .Select(d => d.ParentDocumentId!.Value)
            .ToHashSet();

        return query.Count(d => !parentIds.Contains(d.DocumentId));
    }

    public async Task<AgentDocumentData> CreateAsync(
        int clientId,
        int userId,
        string agentName,
        string title,
        string content,
        string docType)
    {
        var now = DateTimeOffset.UtcNow;
        var nextId = await GetNextDocumentIdAsync(clientId);

        var docData = new
        {
            document_id = nextId,
            agent_name = agentName,
            title = title,
            content_md = content,
            blob_storage_key = (string?)null,
            doc_type = docType,
            version = 1,
            parent_document_id = (int?)null,
            content_size_bytes = System.Text.Encoding.UTF8.GetByteCount(content),
            is_deleted = false,
            created_at = now.ToString("o"),
            created_by = userId,
            updated_at = (string?)null,
            updated_by = (int?)null,
            deleted_at = (string?)null,
            deleted_by = (int?)null
        };

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = userId,
            Collection = CollectionName,
            TableName = TableName,
            Data = JsonSerializer.Serialize(docData),
            CreatedAt = now,
            CreatedBy = userId
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "AGENTDOCS_CREATED: DocId={DocId}, Agent={Agent}, Title={Title}, Type={Type}, ClientId={ClientId}",
            nextId, agentName, title, docType, clientId);

        return new AgentDocumentData
        {
            DocumentId = nextId,
            AgentName = agentName,
            Title = title,
            ContentMd = content,
            DocType = docType,
            Version = 1,
            ContentSizeBytes = System.Text.Encoding.UTF8.GetByteCount(content),
            CreatedAt = now,
            CreatedBy = userId
        };
    }

    public async Task<AgentDocumentData> CreateVersionAsync(
        int clientId,
        int userId,
        int parentDocumentId,
        string? title,
        string? content,
        string? docType)
    {
        // Get parent document
        var parent = await GetByIdAsync(clientId, parentDocumentId);
        if (parent == null)
            throw new InvalidOperationException($"Parent document {parentDocumentId} not found");

        var now = DateTimeOffset.UtcNow;
        var nextId = await GetNextDocumentIdAsync(clientId);

        // Inherit from parent if not specified
        var newTitle = title ?? parent.Title;
        var newContent = content ?? parent.ContentMd ?? string.Empty;
        var newDocType = docType ?? parent.DocType;
        var newVersion = parent.Version + 1;

        var docData = new
        {
            document_id = nextId,
            agent_name = parent.AgentName,
            title = newTitle,
            content_md = newContent,
            blob_storage_key = (string?)null,
            doc_type = newDocType,
            version = newVersion,
            parent_document_id = parentDocumentId,
            content_size_bytes = System.Text.Encoding.UTF8.GetByteCount(newContent),
            is_deleted = false,
            created_at = now.ToString("o"),
            created_by = userId,
            updated_at = (string?)null,
            updated_by = (int?)null,
            deleted_at = (string?)null,
            deleted_by = (int?)null
        };

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = userId,
            Collection = CollectionName,
            TableName = TableName,
            Data = JsonSerializer.Serialize(docData),
            CreatedAt = now,
            CreatedBy = userId
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "AGENTDOCS_VERSION_CREATED: DocId={DocId}, ParentId={ParentId}, Version={Version}, Agent={Agent}, ClientId={ClientId}",
            nextId, parentDocumentId, newVersion, parent.AgentName, clientId);

        return new AgentDocumentData
        {
            DocumentId = nextId,
            AgentName = parent.AgentName,
            Title = newTitle,
            ContentMd = newContent,
            DocType = newDocType,
            Version = newVersion,
            ParentDocumentId = parentDocumentId,
            ContentSizeBytes = System.Text.Encoding.UTF8.GetByteCount(newContent),
            CreatedAt = now,
            CreatedBy = userId
        };
    }

    public async Task<bool> SoftDeleteAsync(int clientId, int documentId, int userId)
    {
        var vibeDocuments = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        foreach (var vibeDoc in vibeDocuments)
        {
            var data = TryDeserialize<AgentDocDataInternal>(vibeDoc.Data);
            if (data?.DocumentId == documentId)
            {
                // Update the JSON data to mark as deleted
                data.IsDeleted = true;
                data.DeletedAt = DateTimeOffset.UtcNow.ToString("o");
                data.DeletedBy = userId;

                vibeDoc.Data = JsonSerializer.Serialize(data);
                vibeDoc.DeletedAt = DateTimeOffset.UtcNow;
                vibeDoc.UpdatedBy = userId;

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "AGENTDOCS_DELETED: DocId={DocId}, ClientId={ClientId}, DeletedBy={UserId}",
                    documentId, clientId, userId);

                return true;
            }
        }

        return false;
    }

    public async Task<List<AgentDocumentData>> GetVersionHistoryAsync(int clientId, int documentId)
    {
        var allDocs = await GetAllDocumentsAsync(clientId);
        var result = new List<AgentDocumentData>();

        // Start with the requested document
        var current = allDocs.FirstOrDefault(d => d.DocumentId == documentId);
        if (current == null) return result;

        result.Add(current);

        // Walk up the parent chain
        while (current.ParentDocumentId.HasValue)
        {
            current = allDocs.FirstOrDefault(d => d.DocumentId == current.ParentDocumentId.Value);
            if (current == null) break;
            result.Add(current);
        }

        // Also find any children (newer versions)
        var children = FindChildren(allDocs, documentId);
        result.AddRange(children);

        // Return ordered by version descending
        return result
            .DistinctBy(d => d.DocumentId)
            .OrderByDescending(d => d.Version)
            .ToList();
    }

    #region Private Methods

    private List<AgentDocumentData> FindChildren(List<AgentDocumentData> allDocs, int parentId)
    {
        var children = new List<AgentDocumentData>();
        var directChildren = allDocs.Where(d => d.ParentDocumentId == parentId).ToList();

        foreach (var child in directChildren)
        {
            children.Add(child);
            children.AddRange(FindChildren(allDocs, child.DocumentId));
        }

        return children;
    }

    private async Task<List<AgentDocumentData>> GetAllDocumentsAsync(int clientId)
    {
        var vibeDocuments = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == TableName)
            .ToListAsync();

        return vibeDocuments
            .Select(d => ParseDocumentData(d.Data))
            .Where(d => d != null)
            .Cast<AgentDocumentData>()
            .ToList();
    }

    private async Task<int> GetNextDocumentIdAsync(int clientId)
    {
        var documents = await GetAllDocumentsAsync(clientId);
        if (!documents.Any())
            return 1;

        return documents.Max(d => d.DocumentId) + 1;
    }

    private static AgentDocumentData? ParseDocumentData(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            var data = JsonSerializer.Deserialize<AgentDocDataInternal>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data == null) return null;

            return new AgentDocumentData
            {
                DocumentId = data.DocumentId,
                AgentName = data.AgentName ?? string.Empty,
                Title = data.Title ?? string.Empty,
                ContentMd = data.ContentMd,
                BlobStorageKey = data.BlobStorageKey,
                DocType = data.DocType ?? "draft",
                Version = data.Version,
                ParentDocumentId = data.ParentDocumentId,
                ContentSizeBytes = data.ContentSizeBytes,
                IsDeleted = data.IsDeleted,
                CreatedAt = DateTimeOffset.TryParse(data.CreatedAt, out var ca) ? ca : DateTimeOffset.MinValue,
                CreatedBy = data.CreatedBy,
                UpdatedAt = DateTimeOffset.TryParse(data.UpdatedAt, out var ua) ? ua : null,
                UpdatedBy = data.UpdatedBy,
                DeletedAt = DateTimeOffset.TryParse(data.DeletedAt, out var da) ? da : null,
                DeletedBy = data.DeletedBy
            };
        }
        catch
        {
            return null;
        }
    }

    private static T? TryDeserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    // Internal class for JSON deserialization with snake_case support
    private class AgentDocDataInternal
    {
        public int DocumentId { get; set; }
        public string? AgentName { get; set; }
        public string? Title { get; set; }
        public string? ContentMd { get; set; }
        public string? BlobStorageKey { get; set; }
        public string? DocType { get; set; }
        public int Version { get; set; } = 1;
        public int? ParentDocumentId { get; set; }
        public long ContentSizeBytes { get; set; }
        public bool IsDeleted { get; set; }
        public string? CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
        public string? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
        public string? DeletedAt { get; set; }
        public int? DeletedBy { get; set; }
    }

    #endregion
}
