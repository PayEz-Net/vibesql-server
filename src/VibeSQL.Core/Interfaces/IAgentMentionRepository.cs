using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for agent_mail_mentions table operations.
/// Handles persistence of @mention records in agent mail messages.
/// </summary>
public interface IAgentMentionRepository
{
    /// <summary>
    /// Get a mention by its UUID.
    /// </summary>
    Task<VibeDocument?> GetMentionByIdAsync(int clientId, string mentionId);

    /// <summary>
    /// Get all mentions for a specific agent with optional filtering.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="agentId">The agent who was mentioned</param>
    /// <param name="unreadOnly">Only return unread mentions</param>
    /// <param name="since">Only return mentions after this timestamp</param>
    /// <param name="threadId">Filter by thread</param>
    /// <param name="limit">Maximum results to return</param>
    /// <param name="offset">Offset for pagination</param>
    Task<List<VibeDocument>> GetMentionsForAgentAsync(
        int clientId,
        int agentId,
        bool unreadOnly = false,
        DateTimeOffset? since = null,
        string? threadId = null,
        int limit = 50,
        int offset = 0);

    /// <summary>
    /// Get total mention count for an agent (for pagination).
    /// </summary>
    Task<int> GetMentionCountForAgentAsync(
        int clientId,
        int agentId,
        bool unreadOnly = false,
        DateTimeOffset? since = null,
        string? threadId = null);

    /// <summary>
    /// Get unread mention count for an agent.
    /// </summary>
    Task<int> GetUnreadMentionCountAsync(int clientId, int agentId);

    /// <summary>
    /// Check if a mention already exists for a message and agent combination.
    /// </summary>
    Task<bool> MentionExistsAsync(int clientId, int messageId, int mentionedAgentId);

    /// <summary>
    /// Create a new mention record.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="messageId">The message containing the mention</param>
    /// <param name="mentionedAgentId">The agent who was mentioned</param>
    /// <returns>Created mention document</returns>
    Task<VibeDocument> CreateMentionAsync(int clientId, int messageId, int mentionedAgentId);

    /// <summary>
    /// Create multiple mention records for a message (batch insert).
    /// </summary>
    Task<int> CreateMentionsBatchAsync(int clientId, int messageId, IEnumerable<int> mentionedAgentIds);

    /// <summary>
    /// Mark a mention as read.
    /// </summary>
    Task<bool> MarkAsReadAsync(int clientId, string mentionId);

    /// <summary>
    /// Mark all mentions for an agent as read, optionally before a timestamp.
    /// </summary>
    Task<int> MarkAllAsReadAsync(int clientId, int agentId, DateTimeOffset? before = null);

    /// <summary>
    /// Delete all mentions for a message (used when message is edited or deleted).
    /// </summary>
    Task<int> DeleteMentionsForMessageAsync(int clientId, int messageId);

    /// <summary>
    /// Delete a specific mention.
    /// </summary>
    Task<bool> DeleteMentionAsync(int clientId, string mentionId);
}
