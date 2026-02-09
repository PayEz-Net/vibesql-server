using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentDocs;

/// <summary>
/// Internal model for agent document stored in VibeDocument.Data JSON.
/// Follows the vibe_agents.agent_documents schema.
/// </summary>
public class AgentDocumentDataModel
{
    [JsonPropertyName("document_id")]
    public int DocumentId { get; set; }

    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("content_md")]
    public string? ContentMd { get; set; }

    [JsonPropertyName("blob_storage_key")]
    public string? BlobStorageKey { get; set; }

    [JsonPropertyName("doc_type")]
    public string DocType { get; set; } = "draft";

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("parent_document_id")]
    public int? ParentDocumentId { get; set; }

    [JsonPropertyName("content_size_bytes")]
    public long ContentSizeBytes { get; set; }

    [JsonPropertyName("is_deleted")]
    public bool IsDeleted { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("created_by")]
    public int? CreatedBy { get; set; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }

    [JsonPropertyName("updated_by")]
    public int? UpdatedBy { get; set; }

    [JsonPropertyName("deleted_at")]
    public string? DeletedAt { get; set; }

    [JsonPropertyName("deleted_by")]
    public int? DeletedBy { get; set; }
}
