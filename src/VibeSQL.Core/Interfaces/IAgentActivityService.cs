using VibeSQL.Core.DTOs.AgentMail;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for agent activity feed operations.
/// Handles activity logging, querying, aggregation, and real-time streaming.
/// </summary>
public interface IAgentActivityService
{
    /// <summary>
    /// Log an activity event for an agent.
    /// Broadcasts to real-time subscribers after logging.
    /// </summary>
    /// <param name="clientId">Client ID scope</param>
    /// <param name="userId">User ID for authorization</param>
    /// <param name="request">Activity details</param>
    /// <returns>Result with activity ID or error</returns>
    Task<LogActivityResult> LogActivityAsync(
        string clientId,
        int userId,
        LogActivityRequest request);

    /// <summary>
    /// Get activity feed with optional filtering and aggregation.
    /// </summary>
    /// <param name="clientId">Client ID scope</param>
    /// <param name="userId">User ID for authorization (0 for admin mode)</param>
    /// <param name="query">Query parameters (filters, pagination, aggregation)</param>
    /// <returns>Activity feed result with pagination cursor</returns>
    Task<ActivityFeedResult> GetActivityFeedAsync(
        string clientId,
        int userId,
        ActivityFeedQuery query);

    /// <summary>
    /// Subscribe to real-time activity events.
    /// Returns an IAsyncEnumerable for streaming activities.
    /// </summary>
    /// <param name="clientId">Client ID scope</param>
    /// <param name="userId">User ID for authorization</param>
    /// <param name="filters">Optional filters for agents/types</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of activity events</returns>
    IAsyncEnumerable<ActivityStreamEvent> SubscribeAsync(
        string clientId,
        int userId,
        ActivityStreamFilters? filters,
        CancellationToken cancellationToken);

    /// <summary>
    /// Generate a human-readable summary for an activity.
    /// </summary>
    /// <param name="activity">Activity item</param>
    /// <returns>Summary string</returns>
    string GenerateSummary(ActivityItemDto activity);

    /// <summary>
    /// Cleanup old activities based on retention policy.
    /// </summary>
    /// <param name="clientId">Client ID scope</param>
    /// <param name="retentionDays">Days to retain (default 30)</param>
    /// <returns>Number of deleted activities</returns>
    Task<int> CleanupOldActivitiesAsync(string clientId, int retentionDays = 30);
}
