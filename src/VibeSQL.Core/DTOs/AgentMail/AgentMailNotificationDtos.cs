// File: VibeSQL.Core.DTOs/Vibe/AgentMail/AgentMailNotificationDtos.cs
// Agent Mail Push Notification DTOs

using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

/// <summary>
/// Real-time notification payload for agent mail events.
/// </summary>
public class AgentMailNotification
{
    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = "new_message";
    
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    
    [JsonPropertyName("notification_id")]
    public string NotificationId { get; set; } = Guid.NewGuid().ToString("N");
    
    [JsonPropertyName("data")]
    public AgentMailNotificationData Data { get; set; } = new();
    
    [JsonPropertyName("metadata")]
    public AgentMailNotificationMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Message data included in notification.
/// </summary>
public class AgentMailNotificationData
{
    [JsonPropertyName("message_id")]
    public int MessageId { get; set; }
    
    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; set; }
    
    [JsonPropertyName("inbox_id")]
    public int InboxId { get; set; }
    
    [JsonPropertyName("from_agent")]
    public string FromAgent { get; set; } = "";
    
    [JsonPropertyName("from_agent_display")]
    public string FromAgentDisplay { get; set; } = "";
    
    [JsonPropertyName("to_agent")]
    public string ToAgent { get; set; } = "";
    
    [JsonPropertyName("subject")]
    public string? Subject { get; set; }
    
    [JsonPropertyName("preview")]
    public string? Preview { get; set; }
    
    [JsonPropertyName("importance")]
    public string Importance { get; set; } = "normal";
    
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Metadata for routing and context.
/// </summary>
public class AgentMailNotificationMetadata
{
    [JsonPropertyName("client_id")]
    public int ClientId { get; set; }
    
    [JsonPropertyName("user_id")]
    public int? UserId { get; set; }
}

/// <summary>
/// Event types for agent mail notifications.
/// </summary>
public static class AgentMailEventTypes
{
    /// <summary>Default for any new inbox entry.</summary>
    public const string NewMessage = "new_message";
    
    /// <summary>Reply in a thread you participated in.</summary>
    public const string AgentResponse = "agent_response";
    
    /// <summary>You were @mentioned.</summary>
    public const string Mention = "mention";
    
    /// <summary>Message marked high/urgent importance.</summary>
    public const string HighImportance = "high_importance";
}

/// <summary>
/// Connection info returned on successful connect.
/// </summary>
public class HubConnectionInfo
{
    [JsonPropertyName("connection_id")]
    public string ConnectionId { get; set; } = "";
    
    [JsonPropertyName("user_id")]
    public int UserId { get; set; }
    
    [JsonPropertyName("connected_at")]
    public DateTimeOffset ConnectedAt { get; set; }
}

/// <summary>
/// Subscription result returned after SubscribeToAgents.
/// </summary>
public class SubscriptionResult
{
    [JsonPropertyName("subscribed")]
    public List<string> Subscribed { get; set; } = new();
    
    [JsonPropertyName("denied")]
    public List<string> Denied { get; set; } = new();
    
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Current subscription status.
/// </summary>
public class SubscriptionStatus
{
    [JsonPropertyName("connection_id")]
    public string ConnectionId { get; set; } = "";
    
    [JsonPropertyName("subscribed_groups")]
    public List<string> SubscribedGroups { get; set; } = new();
    
    [JsonPropertyName("connected_at")]
    public DateTimeOffset? ConnectedAt { get; set; }
}

/// <summary>
/// Error response from hub.
/// </summary>
public class HubError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

/// <summary>
/// Internal tracking of hub connections.
/// Thread-safe for concurrent access from multiple hub methods.
/// </summary>
public class HubConnectionState
{
    public string ConnectionId { get; set; } = "";
    public int UserId { get; set; }
    public int ClientId { get; set; }
    public DateTimeOffset ConnectedAt { get; set; }
    
    /// <summary>
    /// Thread-safe set of subscribed groups (using ConcurrentDictionary as set).
    /// </summary>
    public ConcurrentDictionary<string, byte> SubscribedGroups { get; set; } = new();
    
    /// <summary>
    /// Get subscribed groups as a list (for serialization).
    /// </summary>
    public List<string> GetSubscribedGroupsList() => SubscribedGroups.Keys.ToList();
}
