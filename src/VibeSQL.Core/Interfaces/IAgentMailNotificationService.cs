// File: VibeSQL.Core/Interfaces/IAgentMailNotificationService.cs
// Interface for Agent Mail push notifications

using VibeSQL.Core.DTOs.AgentMail;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for broadcasting real-time notifications when agent mail events occur.
/// Implementations use SignalR to push notifications to connected clients.
/// </summary>
public interface IAgentMailNotificationService
{
    /// <summary>
    /// Notify an agent of a new message in their inbox.
    /// Broadcasts to all connections subscribed to the agent.
    /// </summary>
    /// <param name="clientId">Client/tenant ID</param>
    /// <param name="agentId">Target agent ID</param>
    /// <param name="notification">Notification payload</param>
    Task NotifyAgentAsync(int clientId, int agentId, AgentMailNotification notification);
    
    /// <summary>
    /// Notify a user of activity across all their agents.
    /// Broadcasts to all connections for the user.
    /// </summary>
    /// <param name="clientId">Client/tenant ID</param>
    /// <param name="userId">Target user ID</param>
    /// <param name="notification">Notification payload</param>
    Task NotifyUserAsync(int clientId, int userId, AgentMailNotification notification);
    
    /// <summary>
    /// Notify multiple agents at once (e.g., for CC recipients).
    /// </summary>
    /// <param name="clientId">Client/tenant ID</param>
    /// <param name="agentIds">Target agent IDs</param>
    /// <param name="notification">Notification payload</param>
    Task NotifyAgentsAsync(int clientId, IEnumerable<int> agentIds, AgentMailNotification notification);
    
    /// <summary>
    /// Check if an agent has any active connections.
    /// Useful for determining if real-time delivery is possible.
    /// </summary>
    /// <param name="clientId">Client/tenant ID</param>
    /// <param name="agentId">Agent ID to check</param>
    /// <returns>True if at least one connection is subscribed to the agent</returns>
    Task<bool> IsAgentConnectedAsync(int clientId, int agentId);
    
    /// <summary>
    /// Get count of active connections for a user.
    /// </summary>
    /// <param name="clientId">Client/tenant ID</param>
    /// <param name="userId">User ID to check</param>
    /// <returns>Number of active connections</returns>
    Task<int> GetUserConnectionCountAsync(int clientId, int userId);
    
    /// <summary>
    /// Get total connection statistics (for monitoring).
    /// </summary>
    /// <returns>Connection stats</returns>
    Task<NotificationServiceStats> GetStatsAsync();
}

/// <summary>
/// Statistics for monitoring the notification service.
/// </summary>
public class NotificationServiceStats
{
    public int TotalConnections { get; set; }
    public int TotalAgentSubscriptions { get; set; }
    public int TotalUserSubscriptions { get; set; }
    public long NotificationsSentTotal { get; set; }
    public DateTimeOffset StatsAsOf { get; set; } = DateTimeOffset.UtcNow;
}
