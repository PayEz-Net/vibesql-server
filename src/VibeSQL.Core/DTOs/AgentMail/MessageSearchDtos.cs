using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

#region Request DTOs

/// <summary>
/// Query parameters for message search endpoint.
/// Maps to GET /v1/agentmail/search
/// </summary>
public class MessageSearchQuery
{
    /// <summary>
    /// Search query (keywords, phrases in quotes).
    /// Required, min 2 chars, max 500 chars.
    /// </summary>
    [JsonPropertyName("q")]
    public string Q { get; set; } = "";

    /// <summary>
    /// Filter by sender (email or name, partial match).
    /// </summary>
    [JsonPropertyName("from")]
    public string? From { get; set; }

    /// <summary>
    /// Filter by recipient (email or name, partial match).
    /// </summary>
    [JsonPropertyName("to")]
    public string? To { get; set; }

    /// <summary>
    /// Messages after date (ISO 8601: YYYY-MM-DD).
    /// </summary>
    [JsonPropertyName("after")]
    public string? After { get; set; }

    /// <summary>
    /// Messages before date (ISO 8601: YYYY-MM-DD).
    /// </summary>
    [JsonPropertyName("before")]
    public string? Before { get; set; }

    /// <summary>
    /// Filter by thread/conversation ID.
    /// </summary>
    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; set; }

    /// <summary>
    /// Scope to specific mailbox (agent name).
    /// </summary>
    [JsonPropertyName("mailbox_id")]
    public string? MailboxId { get; set; }

    /// <summary>
    /// Results per page (default: 20, max: 100).
    /// </summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 20;

    /// <summary>
    /// Pagination offset (default: 0).
    /// </summary>
    [JsonPropertyName("offset")]
    public int Offset { get; set; } = 0;

    /// <summary>
    /// Sort order: relevance (default), date_desc, date_asc.
    /// </summary>
    [JsonPropertyName("sort")]
    public string? Sort { get; set; } = "relevance";
}

#endregion

#region Result DTOs

/// <summary>
/// Service result for message search operation.
/// </summary>
public class MessageSearchResult
{
    public bool Success { get; set; }
    public string Error { get; set; } = "";
    public string? ErrorCode { get; set; }
    public List<MessageSearchResultItem> Results { get; set; } = new();
    public int TotalCount { get; set; }
    public bool HasMore { get; set; }
    public List<string> FiltersApplied { get; set; } = new();
}

/// <summary>
/// Individual search result with highlighted matches.
/// </summary>
public class MessageSearchResultItem
{
    [JsonPropertyName("message_id")]
    public int MessageId { get; set; }

    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; set; }

    [JsonPropertyName("mailbox_id")]
    public string? MailboxId { get; set; }

    /// <summary>
    /// Subject with &lt;mark&gt; tags around matches.
    /// </summary>
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = "";

    /// <summary>
    /// ~200 char context snippet with highlighted matches.
    /// </summary>
    [JsonPropertyName("snippet")]
    public string Snippet { get; set; } = "";

    [JsonPropertyName("from")]
    public MessageSearchParticipant? From { get; set; }

    [JsonPropertyName("to")]
    public List<MessageSearchParticipant> To { get; set; } = new();

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("has_attachments")]
    public bool HasAttachments { get; set; }

    /// <summary>
    /// Relevance score (0-1), only when sort=relevance.
    /// </summary>
    [JsonPropertyName("score")]
    public double? Score { get; set; }
}

/// <summary>
/// Participant (sender/recipient) in search results.
/// </summary>
public class MessageSearchParticipant
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";
}

#endregion

#region Response DTOs

/// <summary>
/// API response for search endpoint.
/// </summary>
public class MessageSearchResponse
{
    [JsonPropertyName("results")]
    public List<MessageSearchResultItem> Results { get; set; } = new();

    [JsonPropertyName("pagination")]
    public MessageSearchPagination Pagination { get; set; } = new();

    [JsonPropertyName("query")]
    public MessageSearchQueryInfo Query { get; set; } = new();
}

/// <summary>
/// Pagination info in search response.
/// </summary>
public class MessageSearchPagination
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

/// <summary>
/// Query info echoed back in response.
/// </summary>
public class MessageSearchQueryInfo
{
    [JsonPropertyName("q")]
    public string Q { get; set; } = "";

    [JsonPropertyName("filters_applied")]
    public List<string> FiltersApplied { get; set; } = new();
}

#endregion

#region Error Response

/// <summary>
/// Search error response format.
/// </summary>
public class MessageSearchError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("details")]
    public MessageSearchErrorDetails? Details { get; set; }
}

/// <summary>
/// Error details for validation errors.
/// </summary>
public class MessageSearchErrorDetails
{
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

#endregion
