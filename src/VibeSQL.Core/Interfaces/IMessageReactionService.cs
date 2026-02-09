using VibeSQL.Core.DTOs.AgentMail;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for message reaction operations.
/// Handles business logic, validation, authorization, and rate limiting.
/// </summary>
public interface IMessageReactionService
{
    /// <summary>
    /// Add a reaction to a message.
    /// </summary>
    Task<AddReactionResult> AddReactionAsync(
        string clientId,
        int userId,
        int messageId,
        string agentName,
        AddReactionRequest request);

    /// <summary>
    /// Remove a reaction from a message.
    /// </summary>
    Task<RemoveReactionResult> RemoveReactionAsync(
        string clientId,
        int userId,
        int messageId,
        string agentName,
        string reactionType);

    /// <summary>
    /// List reactions for a message, optionally grouped.
    /// </summary>
    Task<ListReactionsResult> ListReactionsAsync(
        string clientId,
        int userId,
        int messageId,
        bool grouped = true,
        int? currentAgentId = null);

    /// <summary>
    /// Get reaction summary for a message (counts only).
    /// </summary>
    Task<ReactionSummaryResult> GetReactionSummaryAsync(
        string clientId,
        int userId,
        int messageId,
        int? currentAgentId = null);
}
