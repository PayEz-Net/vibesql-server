using System.Text.Json;
using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

// Request DTOs

public class StartSessionRequest
{
    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("machine_name")]
    public string? MachineName { get; set; }
}

// Response/Data DTOs

public class SessionDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("agent_name")]
    public string? AgentName { get; set; }

    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("session_token")]
    public string? SessionToken { get; set; }

    [JsonPropertyName("machine_name")]
    public string? MachineName { get; set; }

    [JsonPropertyName("started_at")]
    public DateTimeOffset? StartedAt { get; set; }

    [JsonPropertyName("last_heartbeat_at")]
    public DateTimeOffset? LastHeartbeatAt { get; set; }

    [JsonPropertyName("ended_at")]
    public DateTimeOffset? EndedAt { get; set; }

    [JsonPropertyName("end_reason")]
    public string? EndReason { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
}

public class ActivityDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("session_id")]
    public int SessionId { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("action_type")]
    public string ActionType { get; set; } = "";

    [JsonPropertyName("target_type")]
    public string? TargetType { get; set; }

    [JsonPropertyName("target_id")]
    public int? TargetId { get; set; }

    [JsonPropertyName("details_json")]
    public JsonElement? DetailsJson { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }
}

// JSON Data Models (for parsing Vibe documents)

public class SessionDataModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("session_token")]
    public string? SessionToken { get; set; }

    [JsonPropertyName("machine_name")]
    public string? MachineName { get; set; }

    [JsonPropertyName("started_at")]
    public string? StartedAt { get; set; }

    [JsonPropertyName("last_heartbeat_at")]
    public string? LastHeartbeatAt { get; set; }

    [JsonPropertyName("ended_at")]
    public string? EndedAt { get; set; }

    [JsonPropertyName("end_reason")]
    public string? EndReason { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;
}

public class ActivityDataModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("session_id")]
    public int SessionId { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("action_type")]
    public string? ActionType { get; set; }

    [JsonPropertyName("target_type")]
    public string? TargetType { get; set; }

    [JsonPropertyName("target_id")]
    public int? TargetId { get; set; }

    [JsonPropertyName("details_json")]
    public JsonElement? DetailsJson { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }
}
