using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

/// <summary>
/// Internal model for agent data stored in VibeDocument.Data JSON field.
/// Used for deserialization from database.
/// </summary>
public class AgentDataModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("owner_user_id")]
    public int OwnerUserId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("program")]
    public string? Program { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("is_shared")]
    public bool? IsShared { get; set; }

    [JsonPropertyName("is_active")]
    public bool? IsActive { get; set; }

    [JsonPropertyName("last_active_at")]
    public string? LastActiveAt { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }
}

/// <summary>
/// Internal model for message data stored in VibeDocument.Data JSON field.
/// Used for deserialization from database.
/// </summary>
public class MessageDataModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("from_agent_id")]
    public int FromAgentId { get; set; }

    [JsonPropertyName("from_user_id")]
    public int FromUserId { get; set; }

    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; set; }

    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("body_format")]
    public string? BodyFormat { get; set; }

    [JsonPropertyName("importance")]
    public string? Importance { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }
}

/// <summary>
/// Internal model for inbox entry data stored in VibeDocument.Data JSON field.
/// Used for deserialization from database.
/// </summary>
public class InboxDataModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("message_id")]
    public int MessageId { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("recipient_type")]
    public string? RecipientType { get; set; }

    [JsonPropertyName("read_at")]
    public string? ReadAt { get; set; }

    [JsonPropertyName("read_by")]
    public int? ReadBy { get; set; }
}

/// <summary>
/// Internal model for message reaction data stored in VibeDocument.Data JSON field.
/// Used for deserialization from database.
/// </summary>
public class MessageReactionDataModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("message_id")]
    public int MessageId { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("reaction_type")]
    public string ReactionType { get; set; } = "";

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }
}

/// <summary>
/// Internal model for message pin data stored in VibeDocument.Data JSON field.
/// Used for deserialization from database.
/// </summary>
public class MessagePinDataModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("message_id")]
    public int MessageId { get; set; }

    [JsonPropertyName("pinned_by_agent_id")]
    public int PinnedByAgentId { get; set; }

    [JsonPropertyName("pin_type")]
    public string PinType { get; set; } = "personal";

    [JsonPropertyName("channel_id")]
    public string? ChannelId { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("position")]
    public int? Position { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }
}

/// <summary>
/// Internal model for message attachment data stored in VibeDocument.Data JSON field.
/// Used for deserialization from database.
/// </summary>
public class MessageAttachmentDataModel
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

    [JsonPropertyName("storage_key")]
    public string StorageKey { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("uploaded_by_agent_id")]
    public int? UploadedByAgentId { get; set; }

    [JsonPropertyName("uploaded_by_user_id")]
    public int UploadedByUserId { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }
}
