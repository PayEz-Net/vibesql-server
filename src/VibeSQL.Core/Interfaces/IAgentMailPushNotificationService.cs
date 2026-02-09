using VibeSQL.Core.DTOs.AgentMail;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for agent mail push notification operations.
/// Handles device token registration, notification preferences, and push delivery.
/// </summary>
public interface IAgentMailPushNotificationService
{
    #region Device Token Management

    /// <summary>
    /// Register a device token for push notifications.
    /// Idempotent - updates existing token if already registered.
    /// </summary>
    Task<RegisterDeviceTokenResult> RegisterDeviceTokenAsync(
        string clientId,
        int userId,
        RegisterDeviceTokenRequest request);

    /// <summary>
    /// Unregister a device token.
    /// </summary>
    Task<UnregisterDeviceTokenResult> UnregisterDeviceTokenAsync(
        string clientId,
        int userId,
        string? deviceId = null,
        string? deviceToken = null);

    /// <summary>
    /// List registered device tokens for a user.
    /// </summary>
    Task<ListDeviceTokensResult> ListDeviceTokensAsync(
        string clientId,
        int userId);

    /// <summary>
    /// Mark a device token as inactive after delivery failures.
    /// </summary>
    Task<bool> MarkDeviceTokenInactiveAsync(
        string clientId,
        string deviceTokenId,
        string? failureReason = null);

    #endregion

    #region Notification Preferences

    /// <summary>
    /// Get notification preferences for an agent.
    /// Returns default preferences if none configured.
    /// </summary>
    Task<GetNotificationPreferencesResult> GetPreferencesAsync(
        string clientId,
        int userId,
        string agentName);

    /// <summary>
    /// Update notification preferences for an agent.
    /// </summary>
    Task<UpdateNotificationPreferencesResult> UpdatePreferencesAsync(
        string clientId,
        int userId,
        string agentName,
        UpdateNotificationPreferencesRequest request);

    /// <summary>
    /// Get notification preferences for all agents owned by a user.
    /// </summary>
    Task<List<NotificationPreferencesDto>> GetAllPreferencesAsync(
        string clientId,
        int userId);

    #endregion

    #region Notification History

    /// <summary>
    /// Get notification history for a user or agent.
    /// </summary>
    Task<NotificationHistoryResult> GetNotificationHistoryAsync(
        string clientId,
        int userId,
        string? agentName = null,
        NotificationHistoryQuery? query = null);

    /// <summary>
    /// Get overall notification status for a user.
    /// </summary>
    Task<NotificationStatusResult> GetNotificationStatusAsync(
        string clientId,
        int userId);

    /// <summary>
    /// Get a specific notification by ID.
    /// </summary>
    Task<NotificationHistoryItemDto?> GetNotificationAsync(
        string clientId,
        int userId,
        string notificationId);

    #endregion

    #region Push Notification Delivery

    /// <summary>
    /// Queue a notification for delivery to an agent's devices.
    /// Called by AgentMailService when a new message arrives.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="agentId">Receiving agent's ID</param>
    /// <param name="eventType">Type of event (new_message, mention, etc.)</param>
    /// <param name="payload">Notification payload data</param>
    /// <returns>Notification ID if queued, null if filtered/skipped</returns>
    Task<string?> QueueNotificationAsync(
        int clientId,
        int agentId,
        string eventType,
        PushNotificationPayload payload);

    /// <summary>
    /// Send a test notification to verify device configuration.
    /// </summary>
    Task<SendTestNotificationResult> SendTestNotificationAsync(
        string clientId,
        int userId,
        SendTestNotificationRequest request);

    /// <summary>
    /// Build a notification payload from mail message data.
    /// </summary>
    PushNotificationPayload BuildNotificationPayload(
        string eventType,
        AgentMailMessageDto message,
        string recipientAgentName,
        int clientId,
        bool includePreview = true);

    #endregion

    #region Utility Methods

    /// <summary>
    /// Check if notifications should be delivered based on preferences and quiet hours.
    /// </summary>
    Task<bool> ShouldDeliverNotificationAsync(
        string clientId,
        int agentId,
        string eventType,
        string importance,
        string? fromAgentName = null,
        string? threadId = null);

    /// <summary>
    /// Clean up old notification history entries.
    /// </summary>
    Task<int> CleanupOldNotificationsAsync(string clientId, TimeSpan retentionPeriod);

    #endregion
}

/// <summary>
/// Interface for the actual push delivery implementation (APNs/FCM).
/// Stubbed initially, to be implemented with actual push infrastructure.
/// </summary>
public interface IPushDeliveryProvider
{
    /// <summary>
    /// Send a push notification to APNs.
    /// </summary>
    Task<PushDeliveryResult> SendApnsAsync(
        string deviceToken,
        PushNotificationPayload payload,
        bool isSandbox,
        string? bundleId = null);

    /// <summary>
    /// Send a push notification to FCM.
    /// </summary>
    Task<PushDeliveryResult> SendFcmAsync(
        string deviceToken,
        PushNotificationPayload payload);

    /// <summary>
    /// Send a web push notification.
    /// </summary>
    Task<PushDeliveryResult> SendWebPushAsync(
        string subscriptionJson,
        PushNotificationPayload payload);
}

/// <summary>
/// Result from push delivery attempt.
/// </summary>
public class PushDeliveryResult
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public bool ShouldRetry { get; set; }
    public bool ShouldDisableToken { get; set; }
}
