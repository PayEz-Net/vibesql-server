using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for agent_mail_messages table operations.
/// Abstracts data access from business logic.
/// </summary>
public interface IAgentMailRepository
{
    Task<VibeDocument?> GetMessageAsync(int clientId, int messageId);
    Task<List<VibeDocument>> GetMessagesByThreadAsync(int clientId, string threadId);
    Task<VibeDocument> CreateMessageAsync(int clientId, int fromAgentId, int fromUserId, string threadId, string subject, string body, string bodyFormat, string importance);

    /// <summary>
    /// Search messages with full-text search and filters.
    /// Returns paginated results with highlighted matches.
    /// </summary>
    /// <param name="clientId">Client ID scope</param>
    /// <param name="query">Search query text</param>
    /// <param name="fromFilter">Filter by sender (partial match)</param>
    /// <param name="toFilter">Filter by recipient (partial match)</param>
    /// <param name="afterDate">Messages after this date</param>
    /// <param name="beforeDate">Messages before this date</param>
    /// <param name="threadId">Filter by thread ID</param>
    /// <param name="mailboxAgentId">Scope to specific mailbox (agent ID)</param>
    /// <param name="sortBy">Sort order: relevance, date_desc, date_asc</param>
    /// <param name="limit">Results per page</param>
    /// <param name="offset">Pagination offset</param>
    /// <returns>List of matching messages with highlights and scores</returns>
    Task<(List<MessageSearchData> Results, int TotalCount)> SearchMessagesAsync(
        int clientId,
        string query,
        string? fromFilter = null,
        string? toFilter = null,
        DateTimeOffset? afterDate = null,
        DateTimeOffset? beforeDate = null,
        string? threadId = null,
        int? mailboxAgentId = null,
        string sortBy = "relevance",
        int limit = 20,
        int offset = 0);
}

/// <summary>
/// Data structure for search results with highlights.
/// </summary>
public class MessageSearchData
{
    public int MessageId { get; set; }
    public string? ThreadId { get; set; }
    public int FromAgentId { get; set; }
    public string? FromAgentName { get; set; }
    public string? FromAgentDisplayName { get; set; }
    public string? Subject { get; set; }
    public string? SubjectHighlighted { get; set; }
    public string? Snippet { get; set; }
    public string? CreatedAt { get; set; }
    public double Score { get; set; }
    public bool HasAttachments { get; set; }
}

/// <summary>
/// Repository for agent_mail_inbox table operations.
/// </summary>
public interface IAgentMailInboxRepository
{
    Task<List<VibeDocument>> GetInboxEntriesAsync(int clientId, int agentId, bool unreadOnly = false);
    Task<VibeDocument?> GetInboxEntryAsync(int clientId, int inboxId);
    Task<VibeDocument> CreateInboxEntryAsync(int clientId, int messageId, int agentId, string recipientType);
    Task<bool> MarkAsReadAsync(int clientId, int inboxId, int? userId = null);
    Task<int> GetUnreadCountAsync(int clientId, int agentId);
    
    /// <summary>
    /// Gets inbox entry by message ID for a specific agent.
    /// </summary>
    Task<VibeDocument?> GetInboxEntryByMessageIdAsync(int clientId, int agentId, int messageId);
    
    /// <summary>
    /// Marks a message as read by message ID (finds the appropriate inbox entry for the agent).
    /// </summary>
    Task<bool> MarkMessageAsReadAsync(int clientId, int agentId, int messageId, int? userId = null);
}

/// <summary>
/// Repository for agent_mail_agents table operations.
/// </summary>
public interface IAgentRepository
{
    Task<VibeDocument?> GetAgentByNameAsync(int clientId, string agentName);
    Task<VibeDocument?> GetAgentByIdAsync(int clientId, int agentId);
    Task<Dictionary<string, VibeDocument>> GetAgentsByNamesAsync(int clientId, IEnumerable<string> agentNames);
    Task<List<VibeDocument>> GetAllAgentsAsync(int clientId);
    Task<List<VibeDocument>> GetAgentsByOwnerAsync(int clientId, int ownerUserId);
    Task<VibeDocument> CreateAgentAsync(int clientId, int ownerUserId, string name, string displayName, string role, string program, string model, bool isShared = false);
    Task<int> GetAgentCountByOwnerAsync(int clientId, int ownerUserId);
}

/// <summary>
/// Repository for agent_mail_reactions table operations.
/// </summary>
public interface IMessageReactionRepository
{
    /// <summary>
    /// Get a reaction by message ID, agent ID, and reaction type.
    /// </summary>
    Task<VibeDocument?> GetReactionAsync(int clientId, int messageId, int agentId, string reactionType);

    /// <summary>
    /// Get all reactions for a message.
    /// </summary>
    Task<List<VibeDocument>> GetReactionsByMessageAsync(int clientId, int messageId);

    /// <summary>
    /// Get all reactions by a specific agent for a message.
    /// </summary>
    Task<List<VibeDocument>> GetReactionsByAgentAsync(int clientId, int messageId, int agentId);

    /// <summary>
    /// Create a new reaction.
    /// </summary>
    Task<VibeDocument> CreateReactionAsync(int clientId, int messageId, int agentId, string reactionType);

    /// <summary>
    /// Delete a reaction.
    /// </summary>
    Task<bool> DeleteReactionAsync(int clientId, int messageId, int agentId, string reactionType);

    /// <summary>
    /// Get reaction counts grouped by reaction type.
    /// </summary>
    Task<Dictionary<string, int>> GetReactionCountsAsync(int clientId, int messageId);
}

/// <summary>
/// Repository for agent_mail_pins table operations.
/// </summary>
public interface IMessagePinRepository
{
    /// <summary>
    /// Get a pin by its UUID.
    /// </summary>
    Task<VibeDocument?> GetPinByIdAsync(int clientId, string pinId);

    /// <summary>
    /// Get all pins for an agent.
    /// </summary>
    Task<List<VibeDocument>> GetPinsForAgentAsync(int clientId, int agentId, string? pinType = null, string? channelId = null, int limit = 50, int offset = 0);

    /// <summary>
    /// Get total pin count for an agent (for pagination).
    /// </summary>
    Task<int> GetPinCountForAgentAsync(int clientId, int agentId, string? pinType = null, string? channelId = null);

    /// <summary>
    /// Find existing pin for a message by the same agent with same type/channel.
    /// </summary>
    Task<VibeDocument?> FindExistingPinAsync(int clientId, int messageId, int agentId, string pinType, string? channelId);

    /// <summary>
    /// Create a new pin.
    /// </summary>
    Task<VibeDocument> CreatePinAsync(int clientId, int messageId, int agentId, string pinType, string? channelId, string? note, int? position);

    /// <summary>
    /// Update a pin's note and/or position.
    /// </summary>
    Task<bool> UpdatePinAsync(int clientId, string pinId, string? note, int? position);

    /// <summary>
    /// Delete a pin by message, agent, type, and optional channel.
    /// </summary>
    Task<bool> DeletePinAsync(int clientId, int messageId, int agentId, string pinType, string? channelId);

    /// <summary>
    /// Delete a pin by its UUID.
    /// </summary>
    Task<bool> DeletePinByIdAsync(int clientId, string pinId);

    /// <summary>
    /// Count personal pins for an agent.
    /// </summary>
    Task<int> CountPersonalPinsAsync(int clientId, int agentId);

    /// <summary>
    /// Count shared pins for a channel.
    /// </summary>
    Task<int> CountSharedPinsForChannelAsync(int clientId, string channelId);
}

/// <summary>
/// Repository for agent_activity_log table operations.
/// Handles storage and retrieval of agent activity events.
/// </summary>
public interface IAgentActivityRepository
{
    /// <summary>
    /// Log a new activity event.
    /// </summary>
    Task<VibeDocument> LogActivityAsync(
        int clientId,
        string agentId,
        string activityType,
        string? targetType,
        string? targetId,
        string? metadataJson);

    /// <summary>
    /// Get activities with optional filtering and pagination.
    /// </summary>
    Task<(List<VibeDocument> Activities, bool HasMore)> GetActivitiesAsync(
        int clientId,
        string? agentId = null,
        string? activityType = null,
        List<string>? activityTypes = null,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null,
        int limit = 50,
        string? cursor = null);

    /// <summary>
    /// Get activities with aggregation for collapsing repeated actions.
    /// Uses window functions to group by agent, type, and time window.
    /// </summary>
    Task<List<VibeDocument>> GetAggregatedActivitiesAsync(
        int clientId,
        string? agentId = null,
        string? activityType = null,
        List<string>? activityTypes = null,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null,
        int limit = 50,
        string? cursor = null);

    /// <summary>
    /// Get a single activity by ID.
    /// </summary>
    Task<VibeDocument?> GetActivityByIdAsync(int clientId, string activityId);

    /// <summary>
    /// Delete activities older than retention period.
    /// </summary>
    Task<int> DeleteOldActivitiesAsync(int clientId, TimeSpan retentionPeriod);

    /// <summary>
    /// Count activities for an agent within a time range.
    /// </summary>
    Task<int> CountActivitiesAsync(
        int clientId,
        string agentId,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null);
}

/// <summary>
/// Repository for agent_presence table operations.
/// </summary>
public interface IAgentPresenceRepository
{
    /// <summary>
    /// Get presence for a specific agent.
    /// </summary>
    Task<VibeDocument?> GetPresenceAsync(int clientId, int agentId);

    /// <summary>
    /// Get presence for multiple agents.
    /// </summary>
    Task<List<VibeDocument>> GetPresenceByAgentIdsAsync(int clientId, IEnumerable<int> agentIds);

    /// <summary>
    /// Get all presence records, optionally filtered by status.
    /// </summary>
    Task<List<VibeDocument>> GetAllPresenceAsync(int clientId, string? status = null, int limit = 100, int offset = 0);

    /// <summary>
    /// Create or update presence for an agent.
    /// </summary>
    Task<VibeDocument> UpsertPresenceAsync(int clientId, int agentId, string status, string? statusMessage, string? clientInfoJson);

    /// <summary>
    /// Update heartbeat timestamp for an agent.
    /// </summary>
    Task<bool> UpdateHeartbeatAsync(int clientId, int agentId);

    /// <summary>
    /// Set agent status to offline.
    /// </summary>
    Task<bool> MarkOfflineAsync(int clientId, int agentId);

    /// <summary>
    /// Get agents with stale heartbeats (older than threshold).
    /// </summary>
    Task<List<VibeDocument>> GetStalePresenceAsync(int clientId, TimeSpan heartbeatThreshold);

    /// <summary>
    /// Count online agents.
    /// </summary>
    Task<int> CountOnlineAgentsAsync(int clientId);

    /// <summary>
    /// Count away agents.
    /// </summary>
    Task<int> CountAwayAgentsAsync(int clientId);
}

/// <summary>
/// Repository for agent_mail_handoffs table operations.
/// Handles storage and retrieval of agent handoff records.
/// </summary>
public interface IAgentHandoffRepository
{
    /// <summary>
    /// Get a handoff by its UUID.
    /// </summary>
    Task<VibeDocument?> GetHandoffByIdAsync(int clientId, string handoffId);

    /// <summary>
    /// Get handoffs with optional filtering and pagination.
    /// </summary>
    /// <param name="clientId">Client ID scope</param>
    /// <param name="status">Filter by status (pending, accepted, declined, expired, all)</param>
    /// <param name="toSessionId">Filter by recipient session ID</param>
    /// <param name="fromSessionId">Filter by sender session ID</param>
    /// <param name="taskId">Filter by task ID</param>
    /// <param name="limit">Max results</param>
    /// <param name="offset">Pagination offset</param>
    /// <returns>List of handoff documents and total count</returns>
    Task<(List<VibeDocument> Handoffs, int Total)> GetHandoffsAsync(
        int clientId,
        string? status = null,
        string? toSessionId = null,
        string? fromSessionId = null,
        string? taskId = null,
        int limit = 20,
        int offset = 0);

    /// <summary>
    /// Get handoff chain/history for a specific task.
    /// </summary>
    Task<List<VibeDocument>> GetHandoffsByTaskIdAsync(int clientId, string taskId);

    /// <summary>
    /// Create a new handoff.
    /// </summary>
    Task<VibeDocument> CreateHandoffAsync(
        int clientId,
        string handoffId,
        string fromSessionId,
        string? toSessionId,
        string taskId,
        string dataJson,
        string? previousHandoffId = null);

    /// <summary>
    /// Update handoff status and related fields.
    /// </summary>
    Task<bool> UpdateHandoffAsync(int clientId, string handoffId, string dataJson);

    /// <summary>
    /// Get pending handoffs for a specific recipient.
    /// </summary>
    Task<List<VibeDocument>> GetPendingHandoffsForRecipientAsync(int clientId, string sessionId);

    /// <summary>
    /// Get open handoffs (no specific recipient).
    /// </summary>
    Task<List<VibeDocument>> GetOpenHandoffsAsync(int clientId);

    /// <summary>
    /// Count pending handoffs from a sender.
    /// </summary>
    Task<int> CountPendingHandoffsFromSenderAsync(int clientId, string sessionId);

    /// <summary>
    /// Mark expired handoffs.
    /// </summary>
    Task<int> MarkExpiredHandoffsAsync(int clientId);
}
