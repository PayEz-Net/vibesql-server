using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

#region Request DTOs

/// <summary>
/// Request to create a pin on a message.
/// </summary>
public class CreatePinRequest
{
    [JsonPropertyName("pin_type")]
    public string PinType { get; set; } = "personal";

    [JsonPropertyName("channel_id")]
    public string? ChannelId { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("position")]
    public int? Position { get; set; }
}

/// <summary>
/// Request to update an existing pin.
/// </summary>
public class UpdatePinRequest
{
    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("position")]
    public int? Position { get; set; }
}

/// <summary>
/// Query parameters for listing pins.
/// </summary>
public class PinsListQuery
{
    public string? Type { get; set; }
    public string? ChannelId { get; set; }
    public int Limit { get; set; } = 25;
    public int Offset { get; set; } = 0;
    public bool IncludeMessage { get; set; } = true;
}

#endregion

#region Result DTOs

/// <summary>
/// Result of a pin operation.
/// </summary>
public class PinResult
{
    public bool Success { get; set; }
    public PinDto? Pin { get; set; }
    public string Error { get; set; } = "";
    public bool AlreadyExists { get; set; }
}

/// <summary>
/// Result of listing pins.
/// </summary>
public class PinsListResponse
{
    [JsonPropertyName("pins")]
    public List<PinDto> Pins { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}

public class PinsListResult
{
    public bool Success { get; set; }
    public PinsListResponse? Data { get; set; }
    public string Error { get; set; } = "";
}

public class UnpinResult
{
    public bool Success { get; set; }
    public string Error { get; set; } = "";
}

#endregion

#region Data DTOs

/// <summary>
/// Pin data transfer object.
/// </summary>
public class PinDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("message_id")]
    public int MessageId { get; set; }

    [JsonPropertyName("pinned_by")]
    public PinnerDto? PinnedBy { get; set; }

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

    /// <summary>
    /// Optionally included message data when include_message=true.
    /// </summary>
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AgentMailMessageDto? Message { get; set; }
}

/// <summary>
/// Pinner info (nested in PinDto).
/// </summary>
public class PinnerDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

#endregion
