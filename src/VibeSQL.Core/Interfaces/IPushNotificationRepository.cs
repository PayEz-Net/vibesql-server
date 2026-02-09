using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for push notification device tokens.
/// </summary>
public interface IDeviceTokenRepository
{
    /// <summary>
    /// Get a device token by its ID.
    /// </summary>
    Task<VibeDocument?> GetByIdAsync(int clientId, string tokenId);

    /// <summary>
    /// Get a device token by the token hash (for deduplication).
    /// </summary>
    Task<VibeDocument?> GetByTokenHashAsync(int clientId, int userId, string tokenHash);

    /// <summary>
    /// Get a device token by device ID.
    /// </summary>
    Task<VibeDocument?> GetByDeviceIdAsync(int clientId, int userId, string deviceId);

    /// <summary>
    /// Get all device tokens for a user.
    /// </summary>
    Task<List<VibeDocument>> GetByUserAsync(int clientId, int userId, bool activeOnly = true);

    /// <summary>
    /// Create a new device token.
    /// </summary>
    Task<VibeDocument> CreateAsync(
        int clientId,
        int userId,
        string deviceToken,
        string tokenHash,
        string platform,
        string? deviceId = null,
        string? deviceName = null,
        string? appBundleId = null,
        bool isSandbox = false);

    /// <summary>
    /// Update a device token (e.g., refresh token, update last used).
    /// </summary>
    Task<bool> UpdateAsync(
        int clientId,
        string tokenId,
        string? newDeviceToken = null,
        string? newTokenHash = null,
        bool? isActive = null,
        string? lastFailureReason = null);

    /// <summary>
    /// Update the last used timestamp.
    /// </summary>
    Task<bool> UpdateLastUsedAsync(int clientId, string tokenId);

    /// <summary>
    /// Record a delivery failure.
    /// </summary>
    Task<bool> RecordFailureAsync(int clientId, string tokenId, string failureReason);

    /// <summary>
    /// Delete a device token.
    /// </summary>
    Task<bool> DeleteAsync(int clientId, string tokenId);

    /// <summary>
    /// Delete all tokens for a user.
    /// </summary>
    Task<int> DeleteByUserAsync(int clientId, int userId);

    /// <summary>
    /// Delete a token by device ID.
    /// </summary>
    Task<bool> DeleteByDeviceIdAsync(int clientId, int userId, string deviceId);

    /// <summary>
    /// Count active tokens for a user.
    /// </summary>
    Task<int> CountActiveTokensAsync(int clientId, int userId);
}

/// <summary>
/// Repository for notification preferences per agent.
/// </summary>
public interface INotificationPreferencesRepository
{
    /// <summary>
    /// Get preferences for an agent.
    /// </summary>
    Task<VibeDocument?> GetByAgentIdAsync(int clientId, int agentId);

    /// <summary>
    /// Get preferences for all agents owned by a user.
    /// </summary>
    Task<List<VibeDocument>> GetByUserAsync(int clientId, int userId);

    /// <summary>
    /// Create or update preferences for an agent.
    /// </summary>
    Task<VibeDocument> UpsertAsync(
        int clientId,
        int agentId,
        bool enabled,
        List<string> eventTypes,
        string minImportance,
        string? quietHoursJson,
        bool includePreview,
        bool soundEnabled,
        bool badgeEnabled,
        List<string> mutedAgents,
        List<string> mutedThreads);

    /// <summary>
    /// Delete preferences for an agent.
    /// </summary>
    Task<bool> DeleteAsync(int clientId, int agentId);

    /// <summary>
    /// Check if an agent is muted by another agent.
    /// </summary>
    Task<bool> IsAgentMutedAsync(int clientId, int recipientAgentId, string fromAgentName);

    /// <summary>
    /// Check if a thread is muted by an agent.
    /// </summary>
    Task<bool> IsThreadMutedAsync(int clientId, int agentId, string threadId);
}

/// <summary>
/// Repository for notification history.
/// </summary>
public interface INotificationHistoryRepository
{
    /// <summary>
    /// Get a notification by ID.
    /// </summary>
    Task<VibeDocument?> GetByIdAsync(int clientId, string notificationId);

    /// <summary>
    /// Get notification history with filtering.
    /// </summary>
    Task<(List<VibeDocument> Notifications, int Total)> GetHistoryAsync(
        int clientId,
        int userId,
        int? agentId = null,
        string? status = null,
        string? eventType = null,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null,
        int limit = 50,
        int offset = 0);

    /// <summary>
    /// Create a notification history entry.
    /// </summary>
    Task<VibeDocument> CreateAsync(
        int clientId,
        int userId,
        int agentId,
        string? deviceTokenId,
        string eventType,
        string status,
        string payloadJson,
        int? messageId = null,
        string? threadId = null,
        string? fromAgent = null,
        string? platform = null,
        string? deviceId = null);

    /// <summary>
    /// Update notification status (sent, delivered, failed).
    /// </summary>
    Task<bool> UpdateStatusAsync(
        int clientId,
        string notificationId,
        string status,
        string? failureReason = null);

    /// <summary>
    /// Count notifications for a user since a date.
    /// </summary>
    Task<int> CountSinceAsync(int clientId, int userId, DateTimeOffset since);

    /// <summary>
    /// Get the most recent notification for a user.
    /// </summary>
    Task<VibeDocument?> GetLatestAsync(int clientId, int userId);

    /// <summary>
    /// Delete old notifications older than retention period.
    /// </summary>
    Task<int> DeleteOlderThanAsync(int clientId, DateTimeOffset cutoff);
}
