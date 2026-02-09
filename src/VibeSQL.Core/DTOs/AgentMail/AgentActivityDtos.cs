using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

#region Activity Types

/// <summary>
/// Supported activity types for agent activity feed.
/// </summary>
public static class AgentActivityTypes
{
    public const string MessageSent = "message_sent";
    public const string MessageReceived = "message_received";
    public const string TaskStarted = "task_started";
    public const string TaskCompleted = "task_completed";
    public const string TaskFailed = "task_failed";
    public const string StatusChanged = "status_changed";
    public const string ToolInvoked = "tool_invoked";
    public const string FileWritten = "file_written";
    public const string ApiCalled = "api_called";
    public const string Error = "error";
}

/// <summary>
/// Target types for activity events.
/// </summary>
public static class AgentActivityTargetTypes
{
    public const string Channel = "channel";
    public const string Task = "task";
    public const string Agent = "agent";
    public const string Tool = "tool";
    public const string File = "file";
    public const string Api = "api";
    public const string System = "system";
}

#endregion

#region Request DTOs

/// <summary>
/// Request to log an agent activity.
/// </summary>
public class LogActivityRequest
{
    [JsonPropertyName("agent_id")]
    public string AgentId { get; set; } = "";

    [JsonPropertyName("activity_type")]
    public string ActivityType { get; set; } = "";

    [JsonPropertyName("target_type")]
    public string? TargetType { get; set; }

    [JsonPropertyName("target_id")]
    public string? TargetId { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Query parameters for activity feed.
/// </summary>
public class ActivityFeedQuery
{
    [JsonPropertyName("agent")]
    public string? Agent { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("types")]
    public List<string>? Types { get; set; }

    [JsonPropertyName("since")]
    public DateTimeOffset? Since { get; set; }

    [JsonPropertyName("until")]
    public DateTimeOffset? Until { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 50;

    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }

    [JsonPropertyName("aggregate")]
    public bool Aggregate { get; set; } = true;

    [JsonPropertyName("expand")]
    public bool Expand { get; set; } = false;
}

/// <summary>
/// WebSocket subscription filters.
/// </summary>
public class ActivityStreamSubscription
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = "subscribe";

    [JsonPropertyName("filters")]
    public ActivityStreamFilters? Filters { get; set; }
}

/// <summary>
/// Filters for activity stream subscription.
/// </summary>
public class ActivityStreamFilters
{
    [JsonPropertyName("agents")]
    public List<string>? Agents { get; set; }

    [JsonPropertyName("types")]
    public List<string>? Types { get; set; }
}

#endregion

#region Response DTOs

/// <summary>
/// Single activity item in the feed.
/// </summary>
public class ActivityItemDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("agent_id")]
    public string AgentId { get; set; } = "";

    [JsonPropertyName("activity_type")]
    public string ActivityType { get; set; } = "";

    [JsonPropertyName("target_type")]
    public string? TargetType { get; set; }

    [JsonPropertyName("target_id")]
    public string? TargetId { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("aggregated")]
    public bool Aggregated { get; set; } = false;

    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("first_at")]
    public string? FirstAt { get; set; }

    [JsonPropertyName("last_at")]
    public string? LastAt { get; set; }

    [JsonPropertyName("items")]
    public List<string>? Items { get; set; }
}

/// <summary>
/// Activity feed response with pagination.
/// </summary>
public class ActivityFeedResponse
{
    [JsonPropertyName("activities")]
    public List<ActivityItemDto> Activities { get; set; } = new();

    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}

/// <summary>
/// Real-time activity event for WebSocket/SSE.
/// </summary>
public class ActivityStreamEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "activity";

    [JsonPropertyName("data")]
    public ActivityItemDto? Data { get; set; }
}

/// <summary>
/// Aggregate update event for real-time streams.
/// </summary>
public class AggregateUpdateEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "aggregate_update";

    [JsonPropertyName("aggregate_id")]
    public string AggregateId { get; set; } = "";

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("last_at")]
    public string LastAt { get; set; } = "";
}

#endregion

#region Result DTOs

/// <summary>
/// Result of logging an activity.
/// </summary>
public class LogActivityResult
{
    public bool Success { get; set; }
    public string? ActivityId { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>
/// Result of querying activity feed.
/// </summary>
public class ActivityFeedResult
{
    public bool Success { get; set; }
    public List<ActivityItemDto> Activities { get; set; } = new();
    public string? Cursor { get; set; }
    public bool HasMore { get; set; }
    public string? ErrorCode { get; set; }
    public string Error { get; set; } = "";
}

#endregion

#region Aggregation Rules

/// <summary>
/// Defines how activities should be aggregated.
/// </summary>
public class AggregationRule
{
    [JsonPropertyName("activity_type")]
    public string ActivityType { get; set; } = "";

    [JsonPropertyName("group_by")]
    public List<string> GroupBy { get; set; } = new();

    [JsonPropertyName("window_seconds")]
    public int WindowSeconds { get; set; }

    [JsonPropertyName("min_count")]
    public int MinCount { get; set; }
}

/// <summary>
/// Default aggregation rules as per spec.
/// </summary>
public static class DefaultAggregationRules
{
    public static readonly List<AggregationRule> Rules = new()
    {
        new AggregationRule
        {
            ActivityType = AgentActivityTypes.MessageSent,
            GroupBy = new List<string> { "agent_id", "metadata.channel", "metadata.recipient" },
            WindowSeconds = 300, // 5 minutes
            MinCount = 2
        },
        new AggregationRule
        {
            ActivityType = AgentActivityTypes.ToolInvoked,
            GroupBy = new List<string> { "agent_id", "metadata.tool" },
            WindowSeconds = 60,
            MinCount = 3
        },
        new AggregationRule
        {
            ActivityType = AgentActivityTypes.FileWritten,
            GroupBy = new List<string> { "agent_id" },
            WindowSeconds = 120,
            MinCount = 3
        }
    };
}

#endregion

#region Internal Data

/// <summary>
/// Raw activity data from repository.
/// </summary>
public class ActivityData
{
    public string Id { get; set; } = "";
    public string AgentId { get; set; } = "";
    public string ActivityType { get; set; } = "";
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

#endregion
