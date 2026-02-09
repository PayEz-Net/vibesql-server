using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

#region Request DTOs

/// <summary>
/// Request to add a reaction to a message.
/// </summary>
public class AddReactionRequest
{
    [JsonPropertyName("reaction_type")]
    public string ReactionType { get; set; } = "";
}

#endregion

#region Result DTOs

/// <summary>
/// Result from adding a reaction.
/// </summary>
public class AddReactionResult
{
    public bool Success { get; set; }
    public int ReactionId { get; set; }
    public string Error { get; set; } = "";
    public bool AlreadyExists { get; set; }
}

/// <summary>
/// Result from removing a reaction.
/// </summary>
public class RemoveReactionResult
{
    public bool Success { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>
/// Result from listing reactions.
/// </summary>
public class ListReactionsResult
{
    public bool Success { get; set; }
    public List<ReactionGroupDto> Reactions { get; set; } = new();
    public int TotalCount { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>
/// Result from getting reaction summary.
/// </summary>
public class ReactionSummaryResult
{
    public bool Success { get; set; }
    public int MessageId { get; set; }
    public Dictionary<string, int> Reactions { get; set; } = new();
    public List<string> CurrentAgentReactions { get; set; } = new();
    public int Total { get; set; }
    public string Error { get; set; } = "";
}

#endregion

#region Data DTOs

/// <summary>
/// Individual reaction DTO.
/// </summary>
public class ReactionDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("message_id")]
    public int MessageId { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = "";

    [JsonPropertyName("agent_display_name")]
    public string AgentDisplayName { get; set; } = "";

    [JsonPropertyName("reaction_type")]
    public string ReactionType { get; set; } = "";

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }
}

/// <summary>
/// Reaction group with count and agent info.
/// </summary>
public class ReactionGroupDto
{
    [JsonPropertyName("reaction_type")]
    public string ReactionType { get; set; } = "";

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("current_agent_reacted")]
    public bool CurrentAgentReacted { get; set; }

    [JsonPropertyName("agents")]
    public List<ReactionAgentDto> Agents { get; set; } = new();
}

/// <summary>
/// Minimal agent info for reaction display.
/// </summary>
public class ReactionAgentDto
{
    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = "";

    [JsonPropertyName("agent_display_name")]
    public string AgentDisplayName { get; set; } = "";
}

/// <summary>
/// Response for listing reactions grouped.
/// </summary>
public class ReactionsListResponse
{
    [JsonPropertyName("reactions")]
    public List<ReactionGroupDto> Reactions { get; set; } = new();

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }
}

/// <summary>
/// Response for reaction summary.
/// </summary>
public class ReactionSummaryResponse
{
    [JsonPropertyName("message_id")]
    public int MessageId { get; set; }

    [JsonPropertyName("reactions")]
    public Dictionary<string, int> Reactions { get; set; } = new();

    [JsonPropertyName("current_agent_reactions")]
    public List<string> CurrentAgentReactions { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

#endregion
