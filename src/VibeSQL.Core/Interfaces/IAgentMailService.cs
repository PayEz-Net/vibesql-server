using VibeSQL.Core.DTOs.AgentMail;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for agent-to-agent communication via mail system.
/// Handles business logic, validation, authorization, and rate limiting.
/// </summary>
public interface IAgentMailService
{
    /// <summary>
    /// Send a message from one agent to one or more recipients.
    /// </summary>
    Task<AgentMailSendResult> SendMailAsync(string clientId, int userId, AgentMailSendRequest request);

    /// <summary>
    /// Get inbox for an agent with optional filtering.
    /// </summary>
    Task<AgentMailInboxResult> GetInboxAsync(
        string clientId,
        int userId,
        string agentName,
        bool unreadOnly = false,
        string? importance = null,
        int page = 1,
        int pageSize = 20);

    /// <summary>
    /// Get a specific message by ID.
    /// </summary>
    Task<AgentMailMessageResult> GetMessageAsync(string clientId, int userId, int messageId);

    /// <summary>
    /// Mark an inbox entry as read.
    /// </summary>
    Task<AgentMailMarkReadResult> MarkAsReadAsync(string clientId, int userId, int inboxId);

    /// <summary>
    /// Mark a message as read by message ID (finds appropriate inbox entry for the agent).
    /// </summary>
    Task<AgentMailMarkReadResult> MarkMessageAsReadAsync(string clientId, int agentId, int messageId, int? userId = null);

    /// <summary>
    /// List all agents accessible by the user.
    /// </summary>
    Task<List<AgentMailAgentDto>> ListAgentsAsync(string clientId, int userId);

    /// <summary>
    /// Register a new agent for the user.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="userId">User ID</param>
    /// <param name="request">Agent registration request</param>
    /// <param name="tierKey">User's subscription tier key for limit enforcement (optional)</param>
    Task<AgentMailAgentResult> RegisterAgentAsync(string clientId, int userId, AgentMailRegisterRequest request, string? tierKey = null);

    /// <summary>
    /// Search messages with full-text search and filters.
    /// </summary>
    /// <param name="clientId">Client ID scope</param>
    /// <param name="userId">User ID for authorization</param>
    /// <param name="query">Search query parameters</param>
    /// <returns>Search results with highlighted matches and pagination</returns>
    Task<MessageSearchResult> SearchMessagesAsync(string clientId, int userId, MessageSearchQuery query);
}
