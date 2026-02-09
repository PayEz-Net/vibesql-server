using System.Text.Json;
using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

#region Enums

/// <summary>
/// Presence status values.
/// </summary>
public enum PresenceStatus
{
    Online,
    Away,
    Offline
}

#endregion

#region Request DTOs

/// <summary>
/// Request to update agent presence.
/// </summary>
public class UpdatePresenceRequest
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("status_message")]
    public string? StatusMessage { get; set; }

    [JsonPropertyName("client_info")]
    public PresenceClientInfoDto? ClientInfo { get; set; }
}

/// <summary>
/// Request to signal typing status.
/// </summary>
public class TypingRequest
{
    [JsonPropertyName("thread_id")]
    public string ThreadId { get; set; } = "";

    [JsonPropertyName("typing")]
    public bool Typing { get; set; }
}

/// <summary>
/// Query parameters for listing presence.
/// </summary>
public class ListPresenceQuery
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("agent_ids")]
    public string? AgentIds { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 100;

    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

#endregion

#region Result DTOs

/// <summary>
/// Result from updating presence.
/// </summary>
public class UpdatePresenceResult
{
    public bool Success { get; set; }
    public PresenceDto? Presence { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>
/// Result from listing presence.
/// </summary>
public class ListPresenceResult
{
    public bool Success { get; set; }
    public List<PresenceDto> Presence { get; set; } = new();
    public string? NextCursor { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>
/// Result from getting single presence.
/// </summary>
public class GetPresenceResult
{
    public bool Success { get; set; }
    public PresenceDto? Presence { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>
/// Result from heartbeat.
/// </summary>
public class HeartbeatResult
{
    public bool Success { get; set; }
    public string? LastHeartbeatAt { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>
/// Result from typing signal.
/// </summary>
public class TypingResult
{
    public bool Success { get; set; }
    public string? ExpiresAt { get; set; }
    public string Error { get; set; } = "";
}

#endregion

#region Data DTOs

/// <summary>
/// Agent presence DTO.
/// </summary>
public class PresenceDto
{
    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "offline";

    [JsonPropertyName("status_message")]
    public string? StatusMessage { get; set; }

    [JsonPropertyName("last_seen")]
    public string? LastSeen { get; set; }

    [JsonPropertyName("connected_at")]
    public string? ConnectedAt { get; set; }

    [JsonPropertyName("client_info")]
    public PresenceClientInfoDto? ClientInfo { get; set; }
}

/// <summary>
/// Client info for presence tracking.
/// </summary>
public class PresenceClientInfoDto
{
    [JsonPropertyName("user_agent")]
    public string? UserAgent { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

/// <summary>
/// Typing state DTO.
/// </summary>
public class TypingStateDto
{
    [JsonPropertyName("thread_id")]
    public string ThreadId { get; set; } = "";

    [JsonPropertyName("started_at")]
    public string? StartedAt { get; set; }

    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; set; }
}

#endregion

#region Data Models (for Vibe document storage)

/// <summary>
/// Presence data model stored in Vibe documents.
/// </summary>
public class PresenceDataModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "offline";

    [JsonPropertyName("status_message")]
    public string? StatusMessage { get; set; }

    [JsonPropertyName("last_seen")]
    public string? LastSeen { get; set; }

    [JsonPropertyName("last_heartbeat_at")]
    public string? LastHeartbeatAt { get; set; }

    [JsonPropertyName("connected_at")]
    public string? ConnectedAt { get; set; }

    [JsonPropertyName("client_info")]
    public JsonElement? ClientInfo { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }

    public PresenceDto ToDto(string? agentName = null)
    {
        PresenceClientInfoDto? clientInfo = null;
        if (ClientInfo.HasValue && ClientInfo.Value.ValueKind != JsonValueKind.Null)
        {
            try
            {
                clientInfo = JsonSerializer.Deserialize<PresenceClientInfoDto>(ClientInfo.Value.GetRawText());
            }
            catch { }
        }

        return new PresenceDto
        {
            AgentId = AgentId,
            AgentName = agentName ?? "",
            Status = Status,
            StatusMessage = StatusMessage,
            LastSeen = LastSeen,
            ConnectedAt = ConnectedAt,
            ClientInfo = clientInfo
        };
    }
}

/// <summary>
/// Response for list presence endpoint.
/// </summary>
public class PresenceListResponse
{
    [JsonPropertyName("presence")]
    public List<PresenceDto> Presence { get; set; } = new();

    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; set; }
}

#endregion
