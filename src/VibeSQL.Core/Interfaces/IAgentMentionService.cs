using VibeSQL.Core.DTOs.AgentMail;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for handling @mentions in agent mail messages.
/// Provides parsing, storage, retrieval, and notification functionality.
/// </summary>
public interface IAgentMentionService
{
    /// <summary>
    /// Parse @mentions from message body text.
    /// </summary>
    /// <param name="body">Message body text</param>
    /// <returns>List of parsed mentions</returns>
    List<ParsedMention> ParseMentions(string body);

    /// <summary>
    /// Process mentions for a newly created or updated message.
    /// Parses body, resolves agent names, creates mention records, and triggers notifications.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="messageId">Message ID</param>
    /// <param name="senderAgentId">Agent who sent the message</param>
    /// <param name="senderAgentName">Display name of sender agent</param>
    /// <param name="body">Message body text</param>
    /// <param name="threadId">Optional thread ID</param>
    Task<ProcessMentionsResult> ProcessMessageMentionsAsync(
        string clientId,
        int messageId,
        int senderAgentId,
        string senderAgentName,
        string body,
        string? threadId = null);

    /// <summary>
    /// Re-process mentions for an edited message.
    /// Removes existing mentions and processes new ones.
    /// </summary>
    Task<ProcessMentionsResult> ReprocessMessageMentionsAsync(
        string clientId,
        int messageId,
        int senderAgentId,
        string senderAgentName,
        string body,
        string? threadId = null);

    /// <summary>
    /// Get mentions for an agent.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="userId">User ID for authorization</param>
    /// <param name="agentName">Agent name to get mentions for</param>
    /// <param name="query">Query parameters (limit, offset, since, etc.)</param>
    Task<AgentMentionsListResult> GetMentionsForAgentAsync(
        string clientId,
        int userId,
        string agentName,
        AgentMentionsListQuery query);

    /// <summary>
    /// Get unread mention count for an agent.
    /// </summary>
    Task<int> GetUnreadMentionCountAsync(string clientId, int agentId);

    /// <summary>
    /// Mark a specific mention as read.
    /// </summary>
    Task<AgentMentionMarkReadResult> MarkMentionAsReadAsync(
        string clientId,
        int userId,
        string agentName,
        string mentionId);

    /// <summary>
    /// Mark all mentions for an agent as read.
    /// </summary>
    Task<AgentMentionMarkAllReadResult> MarkAllMentionsAsReadAsync(
        string clientId,
        int userId,
        string agentName,
        DateTimeOffset? before = null);

    /// <summary>
    /// Extract all unique agent names mentioned in a body.
    /// Does not validate if agents exist.
    /// </summary>
    List<string> ExtractMentionedAgentNames(string body);
}
