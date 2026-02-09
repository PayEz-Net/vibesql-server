using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

#region Request DTOs

/// <summary>
/// Request to upload an attachment to a message.
/// </summary>
public class AgentMailAttachmentUploadRequest
{
    /// <summary>
    /// Message ID to attach the file to.
    /// </summary>
    [JsonPropertyName("message_id")]
    public int MessageId { get; set; }

    /// <summary>
    /// Original filename.
    /// </summary>
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = "";

    /// <summary>
    /// MIME content type (optional, auto-detected if not provided).
    /// </summary>
    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }

    /// <summary>
    /// Optional description of the attachment.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Agent ID uploading the attachment.
    /// </summary>
    [JsonPropertyName("uploaded_by_agent_id")]
    public int? UploadedByAgentId { get; set; }
}

#endregion

#region Result DTOs

/// <summary>
/// Result of attachment upload operation.
/// </summary>
public class AgentMailAttachmentUploadResult
{
    public bool Success { get; set; }
    public int AttachmentId { get; set; }
    public int MessageId { get; set; }
    public string StorageKey { get; set; } = "";
    public string Error { get; set; } = "";
}

/// <summary>
/// Result of attachment download operation.
/// </summary>
public class AgentMailAttachmentDownloadResult
{
    public bool Success { get; set; }
    public Stream? FileStream { get; set; }
    public string Filename { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>
/// Result of listing attachments for a message.
/// </summary>
public class AgentMailAttachmentListResult
{
    public bool Success { get; set; }
    public int MessageId { get; set; }
    public List<AgentMailAttachmentDto> Attachments { get; set; } = new();
    public int TotalCount { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>
/// Result of attachment delete operation.
/// </summary>
public class AgentMailAttachmentDeleteResult
{
    public bool Success { get; set; }
    public int AttachmentId { get; set; }
    public string Error { get; set; } = "";
}

#endregion

#region DTO Classes

/// <summary>
/// DTO for attachment metadata.
/// </summary>
public class AgentMailAttachmentDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("message_id")]
    public int MessageId { get; set; }

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = "";

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = "";

    [JsonPropertyName("size_bytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("uploaded_by_agent_id")]
    public int? UploadedByAgentId { get; set; }

    [JsonPropertyName("uploaded_by_user_id")]
    public int UploadedByUserId { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Download URL (relative path for API).
    /// </summary>
    [JsonPropertyName("download_url")]
    public string? DownloadUrl { get; set; }
}

#endregion
