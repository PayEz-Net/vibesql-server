using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

// ============================================================================
// Request DTOs
// ============================================================================

/// <summary>
/// Request to create a new channel.
/// </summary>
public class CreateChannelRequest
{
    /// <summary>
    /// Unique channel name (lowercase, alphanumeric + hyphens).
    /// Validated: ^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// Optional description of the channel's purpose.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Optional JSON blob for extensibility (e.g., post_restricted).
    /// </summary>
    [JsonPropertyName("settings")]
    public string? Settings { get; set; }
}

/// <summary>
/// Request to update channel details.
/// </summary>
public class UpdateChannelRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("settings")]
    public string? Settings { get; set; }
}

/// <summary>
/// Request to add a member to a channel.
/// </summary>
public class AddChannelMemberRequest
{
    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    /// <summary>
    /// Role for the new member: 'admin' or 'member'. Defaults to 'member'.
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = "member";
}

/// <summary>
/// Request to update a member's role.
/// </summary>
public class UpdateChannelMemberRequest
{
    /// <summary>
    /// New role: 'admin' or 'member'.
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = "member";
}

/// <summary>
/// Request to post a message to a channel.
/// </summary>
public class PostChannelMessageRequest
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    /// <summary>
    /// Optional metadata: mentions, attachments, priority.
    /// </summary>
    [JsonPropertyName("metadata")]
    public ChannelMessageMetadata? Metadata { get; set; }
}

/// <summary>
/// Metadata for channel messages.
/// </summary>
public class ChannelMessageMetadata
{
    /// <summary>
    /// Agent IDs to highlight/notify.
    /// </summary>
    [JsonPropertyName("mentions")]
    public List<int>? Mentions { get; set; }

    /// <summary>
    /// Attachment references.
    /// </summary>
    [JsonPropertyName("attachments")]
    public List<object>? Attachments { get; set; }

    /// <summary>
    /// Message priority: 'normal' or 'high'.
    /// </summary>
    [JsonPropertyName("priority")]
    public string? Priority { get; set; }
}

/// <summary>
/// Request to mute a channel.
/// </summary>
public class MuteChannelRequest
{
    /// <summary>
    /// Duration in seconds. Null = indefinite.
    /// </summary>
    [JsonPropertyName("duration")]
    public int? Duration { get; set; }
}

/// <summary>
/// Request to mark channel as read.
/// </summary>
public class MarkChannelReadRequest
{
    /// <summary>
    /// Message ID to mark as last read. Null = latest.
    /// </summary>
    [JsonPropertyName("until")]
    public string? Until { get; set; }
}

// ============================================================================
// Response/DTO Classes
// ============================================================================

/// <summary>
/// Channel data returned by API.
/// </summary>
public class ChannelDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("created_by")]
    public int CreatedBy { get; set; }

    [JsonPropertyName("created_by_name")]
    public string? CreatedByName { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("archived")]
    public bool Archived { get; set; }

    [JsonPropertyName("settings")]
    public string? Settings { get; set; }

    [JsonPropertyName("member_count")]
    public int MemberCount { get; set; }

    [JsonPropertyName("unread_count")]
    public int UnreadCount { get; set; }
}

/// <summary>
/// Channel member data returned by API.
/// </summary>
public class ChannelMemberDto
{
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; } = "";

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("agent_name")]
    public string? AgentName { get; set; }

    [JsonPropertyName("agent_display_name")]
    public string? AgentDisplayName { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; } = "member";

    [JsonPropertyName("joined_at")]
    public DateTimeOffset? JoinedAt { get; set; }

    [JsonPropertyName("muted")]
    public bool Muted { get; set; }

    [JsonPropertyName("muted_until")]
    public DateTimeOffset? MutedUntil { get; set; }

    [JsonPropertyName("last_read_at")]
    public DateTimeOffset? LastReadAt { get; set; }
}

/// <summary>
/// Channel message data returned by API.
/// </summary>
public class ChannelMessageDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; } = "";

    [JsonPropertyName("sender_id")]
    public int SenderId { get; set; }

    [JsonPropertyName("sender_name")]
    public string? SenderName { get; set; }

    [JsonPropertyName("sender_display_name")]
    public string? SenderDisplayName { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("metadata")]
    public string? Metadata { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("edited_at")]
    public DateTimeOffset? EditedAt { get; set; }

    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; set; }

    [JsonPropertyName("deleted")]
    public bool Deleted { get; set; }
}

/// <summary>
/// Mute status response.
/// </summary>
public class MuteStatusDto
{
    [JsonPropertyName("muted")]
    public bool Muted { get; set; }

    [JsonPropertyName("muted_until")]
    public DateTimeOffset? MutedUntil { get; set; }
}

// ============================================================================
// JSON Data Models (for Vibe document parsing)
// ============================================================================

/// <summary>
/// Internal model for channel data stored in VibeDocument.Data JSON field.
/// </summary>
public class ChannelDataModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("created_by")]
    public int CreatedBy { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("archived")]
    public bool Archived { get; set; }

    [JsonPropertyName("settings")]
    public string? Settings { get; set; }
}

/// <summary>
/// Internal model for channel member data stored in VibeDocument.Data JSON field.
/// </summary>
public class ChannelMemberDataModel
{
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; } = "";

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; } = "member";

    [JsonPropertyName("joined_at")]
    public string? JoinedAt { get; set; }

    [JsonPropertyName("muted")]
    public bool Muted { get; set; }

    [JsonPropertyName("muted_until")]
    public string? MutedUntil { get; set; }

    [JsonPropertyName("last_read_at")]
    public string? LastReadAt { get; set; }
}

/// <summary>
/// Internal model for channel message data stored in VibeDocument.Data JSON field.
/// </summary>
public class ChannelMessageDataModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; } = "";

    [JsonPropertyName("sender_id")]
    public int SenderId { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("metadata")]
    public string? Metadata { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("edited_at")]
    public string? EditedAt { get; set; }

    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; set; }

    [JsonPropertyName("deleted")]
    public bool Deleted { get; set; }
}
