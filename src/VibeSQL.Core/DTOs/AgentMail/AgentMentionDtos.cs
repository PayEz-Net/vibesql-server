using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

#region Request DTOs

/// <summary>
/// Request parameters for listing mentions.
/// </summary>
public class AgentMentionsListQuery
{
    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 50;

    [JsonPropertyName("offset")]
    public int Offset { get; set; } = 0;

    [JsonPropertyName("since")]
    public DateTimeOffset? Since { get; set; }

    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; set; }

    [JsonPropertyName("include_read")]
    public bool IncludeRead { get; set; } = true;
}

/// <summary>
/// Request to mark mentions as read.
/// </summary>
public class MarkMentionsReadRequest
{
    [JsonPropertyName("before")]
    public DateTimeOffset? Before { get; set; }
}

#endregion

#region Result DTOs

/// <summary>
/// Result of listing mentions for an agent.
/// </summary>
public class AgentMentionsListResult
{
    public bool Success { get; set; }
    public List<AgentMentionDto> Mentions { get; set; } = new();
    public AgentMentionsPaginationDto Pagination { get; set; } = new();
    public string Error { get; set; } = "";
}

/// <summary>
/// Result of marking a mention as read.
/// </summary>
public class AgentMentionMarkReadResult
{
    public bool Success { get; set; }
    public string MentionId { get; set; } = "";
    public DateTimeOffset ReadAt { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>
/// Result of marking all mentions as read.
/// </summary>
public class AgentMentionMarkAllReadResult
{
    public bool Success { get; set; }
    public int MarkedCount { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>
/// Result of processing mentions for a message.
/// </summary>
public class ProcessMentionsResult
{
    public bool Success { get; set; }
    public int MentionCount { get; set; }
    public List<int> MentionedAgentIds { get; set; } = new();
    public string Error { get; set; } = "";
}

#endregion

#region Data DTOs

/// <summary>
/// Individual mention record.
/// </summary>
public class AgentMentionDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("message_id")]
    public int MessageId { get; set; }

    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; set; }

    [JsonPropertyName("mentioned_by")]
    public AgentMentionAgentDto MentionedBy { get; set; } = new();

    [JsonPropertyName("message_preview")]
    public string MessagePreview { get; set; } = "";

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("read")]
    public bool Read { get; set; }

    [JsonPropertyName("read_at")]
    public string? ReadAt { get; set; }
}

/// <summary>
/// Agent info within a mention.
/// </summary>
public class AgentMentionAgentDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "";
}

/// <summary>
/// Pagination info for mentions list.
/// </summary>
public class AgentMentionsPaginationDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}

#endregion

#region Internal Models

/// <summary>
/// Parsed mention from message body.
/// </summary>
public class ParsedMention
{
    /// <summary>
    /// The full mention string as found in text (e.g., "@AgentName").
    /// </summary>
    public string Raw { get; set; } = "";

    /// <summary>
    /// The extracted agent name without @ prefix.
    /// </summary>
    public string AgentName { get; set; } = "";

    /// <summary>
    /// Start position in the message body.
    /// </summary>
    public int StartIndex { get; set; }

    /// <summary>
    /// End position in the message body.
    /// </summary>
    public int EndIndex { get; set; }
}

/// <summary>
/// Resolved mention with agent ID.
/// </summary>
public class ResolvedMention : ParsedMention
{
    /// <summary>
    /// Resolved agent ID from registry.
    /// </summary>
    public int AgentId { get; set; }

    /// <summary>
    /// Whether the agent was found in the registry.
    /// </summary>
    public bool Valid { get; set; }
}

/// <summary>
/// Mention notification payload for push/webhook delivery.
/// </summary>
public class MentionNotification
{
    public string Type { get; set; } = "agent_mention";
    public int RecipientAgentId { get; set; }
    public int MessageId { get; set; }
    public string? ThreadId { get; set; }
    public int MentionedByAgentId { get; set; }
    public string MentionedByName { get; set; } = "";
    public string Preview { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; }
}

#endregion
