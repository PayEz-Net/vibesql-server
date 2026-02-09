// File: VibeSQL.Core.DTOs/Vibe/AgentMail/ReadReceiptDtos.cs
// Read Receipt DTOs for Agent Mail

using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

/// <summary>
/// Information about a reader who has read a message.
/// </summary>
public record ReaderInfo
{
    [JsonPropertyName("agent_id")]
    public int AgentId { get; init; }
    
    [JsonPropertyName("agent_name")]
    public string AgentName { get; init; } = "";
    
    [JsonPropertyName("recipient_type")]
    public string RecipientType { get; init; } = ""; // "to" or "cc"
    
    [JsonPropertyName("read_at")]
    public DateTime ReadAt { get; init; }
}

/// <summary>
/// Information about a recipient who hasn't read the message yet.
/// </summary>
public record PendingReaderInfo
{
    [JsonPropertyName("agent_id")]
    public int AgentId { get; init; }
    
    [JsonPropertyName("agent_name")]
    public string AgentName { get; init; } = "";
    
    [JsonPropertyName("recipient_type")]
    public string RecipientType { get; init; } = "";
    
    [JsonPropertyName("delivered_at")]
    public DateTime DeliveredAt { get; init; }
}

/// <summary>
/// Response for GET /v1/agentmail/messages/{messageId}/readers
/// </summary>
public record MessageReadersResponse
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; init; }
    
    [JsonPropertyName("subject")]
    public string Subject { get; init; } = "";
    
    [JsonPropertyName("sent_at")]
    public DateTime SentAt { get; init; }
    
    [JsonPropertyName("total_recipients")]
    public int TotalRecipients { get; init; }
    
    [JsonPropertyName("read_count")]
    public int ReadCount { get; init; }
    
    [JsonPropertyName("unread_count")]
    public int UnreadCount { get; init; }
    
    [JsonPropertyName("readers")]
    public List<ReaderInfo> Readers { get; init; } = new();
    
    [JsonPropertyName("pending")]
    public List<PendingReaderInfo> Pending { get; init; } = new();
}

/// <summary>
/// Response for POST /v1/agentmail/messages/{messageId}/read
/// </summary>
public record MarkReadResponse
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; init; }
    
    [JsonPropertyName("read_at")]
    public DateTime ReadAt { get; init; }
    
    [JsonPropertyName("receipt_sent")]
    public bool ReceiptSent { get; init; }
}

/// <summary>
/// Request for POST /v1/agentmail/messages/read-status
/// </summary>
public record BulkReadStatusRequest
{
    [JsonPropertyName("message_ids")]
    public required List<long> MessageIds { get; init; }
}

/// <summary>
/// Read status for a single message in bulk response.
/// </summary>
public record MessageReadStatus
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; init; }
    
    [JsonPropertyName("total_recipients")]
    public int TotalRecipients { get; init; }
    
    [JsonPropertyName("read_count")]
    public int ReadCount { get; init; }
    
    [JsonPropertyName("all_read")]
    public bool AllRead { get; init; }
}

/// <summary>
/// Response for POST /v1/agentmail/messages/read-status
/// </summary>
public record BulkReadStatusResponse
{
    [JsonPropertyName("statuses")]
    public List<MessageReadStatus> Statuses { get; init; } = new();
}

/// <summary>
/// Response for GET /v1/agentmail/settings/read-receipts
/// </summary>
public record ReadReceiptSettingsResponse
{
    [JsonPropertyName("send_read_receipts")]
    public bool SendReadReceipts { get; init; }
}

/// <summary>
/// Request for PUT /v1/agentmail/settings/read-receipts
/// </summary>
public record UpdateReadReceiptSettingsRequest
{
    [JsonPropertyName("send_read_receipts")]
    public bool SendReadReceipts { get; init; }
}

/// <summary>
/// Response for PUT /v1/agentmail/settings/read-receipts
/// </summary>
public record UpdateReadReceiptSettingsResponse
{
    [JsonPropertyName("agent_id")]
    public int AgentId { get; init; }
    
    [JsonPropertyName("agent_name")]
    public string AgentName { get; init; } = "";
    
    [JsonPropertyName("send_read_receipts")]
    public bool SendReadReceipts { get; init; }
    
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Real-time notification when a message is read.
/// Sent via SignalR to the message sender.
/// </summary>
public record MessageReadNotification
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "message_read";
    
    [JsonPropertyName("data")]
    public MessageReadNotificationData Data { get; init; } = new();
}

/// <summary>
/// Data payload for message read notification.
/// </summary>
public record MessageReadNotificationData
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; init; }
    
    [JsonPropertyName("reader")]
    public ReaderSummary Reader { get; init; } = new();
    
    [JsonPropertyName("read_at")]
    public DateTime ReadAt { get; init; }
    
    [JsonPropertyName("read_count")]
    public int ReadCount { get; init; }
    
    [JsonPropertyName("total_recipients")]
    public int TotalRecipients { get; init; }
    
    [JsonPropertyName("all_read")]
    public bool AllRead { get; init; }
}

/// <summary>
/// Summary of the reader agent.
/// </summary>
public record ReaderSummary
{
    [JsonPropertyName("agent_id")]
    public int AgentId { get; init; }
    
    [JsonPropertyName("agent_name")]
    public string AgentName { get; init; } = "";
}
