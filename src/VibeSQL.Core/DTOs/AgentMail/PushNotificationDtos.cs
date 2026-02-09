using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

#region Enums

/// <summary>
/// Push notification platform types.
/// </summary>
public enum PushPlatform
{
    /// <summary>Apple Push Notification Service</summary>
    Apns,
    /// <summary>Firebase Cloud Messaging (Android/Web)</summary>
    Fcm,
    /// <summary>Web Push</summary>
    WebPush
}

/// <summary>
/// Notification event types that can trigger push notifications.
/// </summary>
public enum NotificationEventType
{
    NewMessage,
    AgentResponse,
    Mention,
    HighImportance,
    ThreadReply
}

/// <summary>
/// Notification delivery status.
/// </summary>
public enum NotificationDeliveryStatus
{
    Pending,
    Sent,
    Delivered,
    Failed,
    Expired
}

/// <summary>
/// Minimum importance level to trigger notifications.
/// </summary>
public enum NotificationImportanceThreshold
{
    Low,
    Normal,
    High,
    Urgent
}

#endregion

#region Request DTOs

/// <summary>
/// Request to register a device token for push notifications.
/// </summary>
public class RegisterDeviceTokenRequest
{
    /// <summary>
    /// The device token from APNs or FCM.
    /// </summary>
    [JsonPropertyName("device_token")]
    public string DeviceToken { get; set; } = "";

    /// <summary>
    /// Platform: apns, fcm, or web_push.
    /// </summary>
    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "";

    /// <summary>
    /// Optional device identifier for managing multiple devices.
    /// </summary>
    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    /// <summary>
    /// Optional device name for display purposes.
    /// </summary>
    [JsonPropertyName("device_name")]
    public string? DeviceName { get; set; }

    /// <summary>
    /// App bundle identifier (for APNs).
    /// </summary>
    [JsonPropertyName("app_bundle_id")]
    public string? AppBundleId { get; set; }

    /// <summary>
    /// Whether this is a sandbox/development token.
    /// </summary>
    [JsonPropertyName("is_sandbox")]
    public bool IsSandbox { get; set; } = false;
}

/// <summary>
/// Request to update notification preferences for an agent.
/// </summary>
public class UpdateNotificationPreferencesRequest
{
    /// <summary>
    /// Whether push notifications are enabled for this agent.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    /// <summary>
    /// Event types to receive notifications for.
    /// </summary>
    [JsonPropertyName("event_types")]
    public List<string>? EventTypes { get; set; }

    /// <summary>
    /// Minimum importance level to trigger notifications.
    /// Values: low, normal, high, urgent
    /// </summary>
    [JsonPropertyName("min_importance")]
    public string? MinImportance { get; set; }

    /// <summary>
    /// Quiet hours configuration.
    /// </summary>
    [JsonPropertyName("quiet_hours")]
    public QuietHoursConfig? QuietHours { get; set; }

    /// <summary>
    /// Whether to include message preview in notifications.
    /// </summary>
    [JsonPropertyName("include_preview")]
    public bool? IncludePreview { get; set; }

    /// <summary>
    /// Whether to play sound for notifications.
    /// </summary>
    [JsonPropertyName("sound_enabled")]
    public bool? SoundEnabled { get; set; }

    /// <summary>
    /// Whether to show badge count.
    /// </summary>
    [JsonPropertyName("badge_enabled")]
    public bool? BadgeEnabled { get; set; }

    /// <summary>
    /// Specific agents to mute (no notifications from these senders).
    /// </summary>
    [JsonPropertyName("muted_agents")]
    public List<string>? MutedAgents { get; set; }

    /// <summary>
    /// Specific threads to mute.
    /// </summary>
    [JsonPropertyName("muted_threads")]
    public List<string>? MutedThreads { get; set; }
}

/// <summary>
/// Configuration for quiet hours (do not disturb).
/// </summary>
public class QuietHoursConfig
{
    /// <summary>
    /// Whether quiet hours are enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    /// Start time in HH:mm format (24-hour).
    /// </summary>
    [JsonPropertyName("start_time")]
    public string? StartTime { get; set; }

    /// <summary>
    /// End time in HH:mm format (24-hour).
    /// </summary>
    [JsonPropertyName("end_time")]
    public string? EndTime { get; set; }

    /// <summary>
    /// Timezone for quiet hours (e.g., "America/Los_Angeles").
    /// </summary>
    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }

    /// <summary>
    /// Allow high importance notifications during quiet hours.
    /// </summary>
    [JsonPropertyName("allow_high_importance")]
    public bool AllowHighImportance { get; set; } = true;
}

/// <summary>
/// Query parameters for notification history.
/// </summary>
public class NotificationHistoryQuery
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }

    [JsonPropertyName("since")]
    public DateTimeOffset? Since { get; set; }

    [JsonPropertyName("until")]
    public DateTimeOffset? Until { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 50;

    [JsonPropertyName("offset")]
    public int Offset { get; set; } = 0;
}

/// <summary>
/// Request to send a test notification.
/// </summary>
public class SendTestNotificationRequest
{
    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

#endregion

#region Result DTOs

/// <summary>
/// Result from registering a device token.
/// </summary>
public class RegisterDeviceTokenResult
{
    public bool Success { get; set; }
    public DeviceTokenDto? DeviceToken { get; set; }
    public bool AlreadyExists { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>
/// Result from unregistering a device token.
/// </summary>
public class UnregisterDeviceTokenResult
{
    public bool Success { get; set; }
    public int TokensRemoved { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>
/// Result from listing device tokens.
/// </summary>
public class ListDeviceTokensResult
{
    public bool Success { get; set; }
    public List<DeviceTokenDto> Tokens { get; set; } = new();
    public string Error { get; set; } = "";
}

/// <summary>
/// Result from getting notification preferences.
/// </summary>
public class GetNotificationPreferencesResult
{
    public bool Success { get; set; }
    public NotificationPreferencesDto? Preferences { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>
/// Result from updating notification preferences.
/// </summary>
public class UpdateNotificationPreferencesResult
{
    public bool Success { get; set; }
    public NotificationPreferencesDto? Preferences { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>
/// Result from getting notification history.
/// </summary>
public class NotificationHistoryResult
{
    public bool Success { get; set; }
    public List<NotificationHistoryItemDto> Notifications { get; set; } = new();
    public NotificationHistoryPagination? Pagination { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>
/// Result from getting notification status.
/// </summary>
public class NotificationStatusResult
{
    public bool Success { get; set; }
    public NotificationStatusDto? Status { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>
/// Result from sending a test notification.
/// </summary>
public class SendTestNotificationResult
{
    public bool Success { get; set; }
    public string? NotificationId { get; set; }
    public string Error { get; set; } = "";
}

#endregion

#region Data DTOs

/// <summary>
/// Device token DTO for API responses.
/// </summary>
public class DeviceTokenDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("device_name")]
    public string? DeviceName { get; set; }

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "";

    [JsonPropertyName("is_sandbox")]
    public bool IsSandbox { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("last_used_at")]
    public string? LastUsedAt { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }
}

/// <summary>
/// Notification preferences DTO for API responses.
/// </summary>
public class NotificationPreferencesDto
{
    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("event_types")]
    public List<string> EventTypes { get; set; } = new()
    {
        "new_message", "agent_response", "mention", "high_importance"
    };

    [JsonPropertyName("min_importance")]
    public string MinImportance { get; set; } = "low";

    [JsonPropertyName("quiet_hours")]
    public QuietHoursConfig? QuietHours { get; set; }

    [JsonPropertyName("include_preview")]
    public bool IncludePreview { get; set; } = true;

    [JsonPropertyName("sound_enabled")]
    public bool SoundEnabled { get; set; } = true;

    [JsonPropertyName("badge_enabled")]
    public bool BadgeEnabled { get; set; } = true;

    [JsonPropertyName("muted_agents")]
    public List<string> MutedAgents { get; set; } = new();

    [JsonPropertyName("muted_threads")]
    public List<string> MutedThreads { get; set; } = new();

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }
}

/// <summary>
/// Notification history item DTO.
/// </summary>
public class NotificationHistoryItemDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("message_id")]
    public int? MessageId { get; set; }

    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; set; }

    [JsonPropertyName("from_agent")]
    public string? FromAgent { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("sent_at")]
    public string? SentAt { get; set; }

    [JsonPropertyName("delivered_at")]
    public string? DeliveredAt { get; set; }

    [JsonPropertyName("failed_at")]
    public string? FailedAt { get; set; }

    [JsonPropertyName("failure_reason")]
    public string? FailureReason { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }
}

/// <summary>
/// Pagination info for notification history.
/// </summary>
public class NotificationHistoryPagination
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
/// Notification status overview DTO.
/// </summary>
public class NotificationStatusDto
{
    [JsonPropertyName("push_enabled")]
    public bool PushEnabled { get; set; }

    [JsonPropertyName("devices_registered")]
    public int DevicesRegistered { get; set; }

    [JsonPropertyName("active_devices")]
    public int ActiveDevices { get; set; }

    [JsonPropertyName("agents_configured")]
    public int AgentsConfigured { get; set; }

    [JsonPropertyName("in_quiet_hours")]
    public bool InQuietHours { get; set; }

    [JsonPropertyName("last_notification_at")]
    public string? LastNotificationAt { get; set; }

    [JsonPropertyName("notifications_today")]
    public int NotificationsToday { get; set; }

    [JsonPropertyName("notifications_this_week")]
    public int NotificationsThisWeek { get; set; }
}

#endregion

#region Notification Payload DTOs

/// <summary>
/// Push notification payload for sending to devices.
/// </summary>
public class PushNotificationPayload
{
    [JsonPropertyName("notification_id")]
    public string NotificationId { get; set; } = "";

    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("data")]
    public PushNotificationData Data { get; set; } = new();

    [JsonPropertyName("options")]
    public PushNotificationOptions Options { get; set; } = new();
}

/// <summary>
/// Data payload for push notification.
/// </summary>
public class PushNotificationData
{
    [JsonPropertyName("message_id")]
    public int? MessageId { get; set; }

    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; set; }

    [JsonPropertyName("inbox_id")]
    public int? InboxId { get; set; }

    [JsonPropertyName("from_agent")]
    public string? FromAgent { get; set; }

    [JsonPropertyName("from_agent_display")]
    public string? FromAgentDisplay { get; set; }

    [JsonPropertyName("to_agent")]
    public string? ToAgent { get; set; }

    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    [JsonPropertyName("importance")]
    public string Importance { get; set; } = "normal";

    [JsonPropertyName("client_id")]
    public int ClientId { get; set; }

    [JsonPropertyName("deep_link")]
    public string? DeepLink { get; set; }
}

/// <summary>
/// Options for push notification delivery.
/// </summary>
public class PushNotificationOptions
{
    [JsonPropertyName("sound")]
    public string? Sound { get; set; } = "default";

    [JsonPropertyName("badge")]
    public int? Badge { get; set; }

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "high";

    [JsonPropertyName("ttl_seconds")]
    public int TtlSeconds { get; set; } = 86400; // 24 hours

    [JsonPropertyName("collapse_key")]
    public string? CollapseKey { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("mutable_content")]
    public bool MutableContent { get; set; } = true;

    [JsonPropertyName("content_available")]
    public bool ContentAvailable { get; set; } = false;
}

#endregion

#region Data Models (for Vibe document storage)

/// <summary>
/// Device token data model stored in Vibe documents.
/// </summary>
public class DeviceTokenDataModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("device_token")]
    public string DeviceToken { get; set; } = "";

    [JsonPropertyName("device_token_hash")]
    public string DeviceTokenHash { get; set; } = "";

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("device_name")]
    public string? DeviceName { get; set; }

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "";

    [JsonPropertyName("app_bundle_id")]
    public string? AppBundleId { get; set; }

    [JsonPropertyName("is_sandbox")]
    public bool IsSandbox { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("last_used_at")]
    public string? LastUsedAt { get; set; }

    [JsonPropertyName("failure_count")]
    public int FailureCount { get; set; }

    [JsonPropertyName("last_failure_at")]
    public string? LastFailureAt { get; set; }

    [JsonPropertyName("last_failure_reason")]
    public string? LastFailureReason { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }

    public DeviceTokenDto ToDto()
    {
        return new DeviceTokenDto
        {
            Id = Id,
            DeviceId = DeviceId,
            DeviceName = DeviceName,
            Platform = Platform,
            IsSandbox = IsSandbox,
            IsActive = IsActive,
            LastUsedAt = LastUsedAt,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }
}

/// <summary>
/// Notification preferences data model stored in Vibe documents.
/// </summary>
public class NotificationPreferencesDataModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("event_types")]
    public List<string> EventTypes { get; set; } = new()
    {
        "new_message", "agent_response", "mention", "high_importance"
    };

    [JsonPropertyName("min_importance")]
    public string MinImportance { get; set; } = "low";

    [JsonPropertyName("quiet_hours")]
    public QuietHoursConfig? QuietHours { get; set; }

    [JsonPropertyName("include_preview")]
    public bool IncludePreview { get; set; } = true;

    [JsonPropertyName("sound_enabled")]
    public bool SoundEnabled { get; set; } = true;

    [JsonPropertyName("badge_enabled")]
    public bool BadgeEnabled { get; set; } = true;

    [JsonPropertyName("muted_agents")]
    public List<string> MutedAgents { get; set; } = new();

    [JsonPropertyName("muted_threads")]
    public List<string> MutedThreads { get; set; } = new();

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }

    public NotificationPreferencesDto ToDto(string? agentName = null)
    {
        return new NotificationPreferencesDto
        {
            AgentId = AgentId,
            AgentName = agentName ?? "",
            Enabled = Enabled,
            EventTypes = EventTypes,
            MinImportance = MinImportance,
            QuietHours = QuietHours,
            IncludePreview = IncludePreview,
            SoundEnabled = SoundEnabled,
            BadgeEnabled = BadgeEnabled,
            MutedAgents = MutedAgents,
            MutedThreads = MutedThreads,
            UpdatedAt = UpdatedAt
        };
    }
}

/// <summary>
/// Notification history data model stored in Vibe documents.
/// </summary>
public class NotificationHistoryDataModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("device_token_id")]
    public string? DeviceTokenId { get; set; }

    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("payload")]
    public PushNotificationPayload? Payload { get; set; }

    [JsonPropertyName("message_id")]
    public int? MessageId { get; set; }

    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; set; }

    [JsonPropertyName("from_agent")]
    public string? FromAgent { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("sent_at")]
    public string? SentAt { get; set; }

    [JsonPropertyName("delivered_at")]
    public string? DeliveredAt { get; set; }

    [JsonPropertyName("failed_at")]
    public string? FailedAt { get; set; }

    [JsonPropertyName("failure_reason")]
    public string? FailureReason { get; set; }

    [JsonPropertyName("attempts")]
    public int Attempts { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    public NotificationHistoryItemDto ToDto()
    {
        return new NotificationHistoryItemDto
        {
            Id = Id,
            AgentId = AgentId,
            EventType = EventType,
            Status = Status,
            Title = Payload?.Title,
            Body = Payload?.Body,
            MessageId = MessageId,
            ThreadId = ThreadId,
            FromAgent = FromAgent,
            Platform = Platform,
            DeviceId = DeviceId,
            SentAt = SentAt,
            DeliveredAt = DeliveredAt,
            FailedAt = FailedAt,
            FailureReason = FailureReason,
            CreatedAt = CreatedAt
        };
    }
}

#endregion
