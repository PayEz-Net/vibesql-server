using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

#region Request DTOs

public class AgentMailSendRequest
{
    [JsonPropertyName("from_agent")]
    public string FromAgent { get; set; } = "";

    [JsonPropertyName("to")]
    public List<string> To { get; set; } = new();

    [JsonPropertyName("cc")]
    public List<string>? Cc { get; set; }

    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("body_format")]
    public string? BodyFormat { get; set; }

    [JsonPropertyName("importance")]
    public string? Importance { get; set; }

    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; set; }
}

public class AgentMailRegisterRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("program")]
    public string? Program { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }
}

#endregion

#region Result DTOs

public class AgentMailSendResult
{
    public bool Success { get; set; }
    public int MessageId { get; set; }
    public string ThreadId { get; set; } = "";
    public string Error { get; set; } = "";
}

public class AgentMailInboxResult
{
    public bool Success { get; set; }
    public string AgentName { get; set; } = "";
    public List<AgentMailMessageItemDto> Messages { get; set; } = new();
    public int UnreadCount { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public string Error { get; set; } = "";
}

public class AgentMailMessageResult
{
    public bool Success { get; set; }
    public AgentMailMessageDto? Message { get; set; }
    public string Error { get; set; } = "";
}

public class AgentMailMarkReadResult
{
    public bool Success { get; set; }
    public int InboxId { get; set; }
    public int MessageId { get; set; }
    public DateTimeOffset ReadAt { get; set; }
    public string Error { get; set; } = "";
}

public class AgentMailAgentResult
{
    public bool Success { get; set; }
    public AgentMailAgentDto? Agent { get; set; }
    public string Error { get; set; } = "";
    public string? ErrorCode { get; set; }
}

#endregion

#region Data DTOs

public class AgentMailMessageItemDto
{
    [JsonPropertyName("inbox_id")]
    public int InboxId { get; set; }

    [JsonPropertyName("message_id")]
    public int MessageId { get; set; }

    [JsonPropertyName("from_agent")]
    public string FromAgent { get; set; } = "";

    [JsonPropertyName("from_agent_display")]
    public string FromAgentDisplay { get; set; } = "";

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("body_format")]
    public string BodyFormat { get; set; } = "markdown";

    [JsonPropertyName("importance")]
    public string Importance { get; set; } = "normal";

    [JsonPropertyName("recipient_type")]
    public string RecipientType { get; set; } = "to";

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("read_at")]
    public string? ReadAt { get; set; }
}

public class AgentMailMessageDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("from_agent_id")]
    public int FromAgentId { get; set; }

    [JsonPropertyName("from_agent")]
    public string FromAgent { get; set; } = "";

    [JsonPropertyName("from_agent_display")]
    public string FromAgentDisplay { get; set; } = "";

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

public class AgentMailAgentDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("program")]
    public string Program { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("is_shared")]
    public bool IsShared { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("last_active_at")]
    public string? LastActiveAt { get; set; }
}

#endregion
