using VibeSQL.Core.DTOs.AgentMail;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for agent handoff operations.
/// Handles business logic, validation, authorization, and orchestrates repository calls.
/// Enables seamless transfer of task ownership between agents.
/// </summary>
public interface IAgentHandoffService
{
    /// <summary>
    /// Create a new handoff from one agent to another (or open).
    /// </summary>
    /// <param name="clientId">Client ID scope</param>
    /// <param name="fromSessionId">Session ID of the agent initiating handoff</param>
    /// <param name="request">Handoff creation request</param>
    /// <returns>Result with created handoff</returns>
    Task<CreateHandoffResult> CreateHandoffAsync(
        string clientId,
        string fromSessionId,
        CreateHandoffRequest request);

    /// <summary>
    /// Get a specific handoff by ID.
    /// </summary>
    /// <param name="clientId">Client ID scope</param>
    /// <param name="handoffId">Handoff UUID</param>
    /// <returns>Result with full handoff details</returns>
    Task<GetHandoffResult> GetHandoffAsync(string clientId, string handoffId);

    /// <summary>
    /// List handoffs with optional filtering.
    /// </summary>
    /// <param name="clientId">Client ID scope</param>
    /// <param name="query">Query parameters for filtering</param>
    /// <returns>Result with list of handoff summaries</returns>
    Task<ListHandoffsResult> ListHandoffsAsync(string clientId, ListHandoffsQuery query);

    /// <summary>
    /// Accept a pending handoff.
    /// </summary>
    /// <param name="clientId">Client ID scope</param>
    /// <param name="handoffId">Handoff UUID to accept</param>
    /// <param name="acceptingSessionId">Session ID of the accepting agent</param>
    /// <param name="request">Acceptance details</param>
    /// <returns>Result with updated handoff</returns>
    Task<AcceptHandoffResult> AcceptHandoffAsync(
        string clientId,
        string handoffId,
        string acceptingSessionId,
        AcceptHandoffRequest request);

    /// <summary>
    /// Decline a pending handoff.
    /// </summary>
    /// <param name="clientId">Client ID scope</param>
    /// <param name="handoffId">Handoff UUID to decline</param>
    /// <param name="decliningSessionId">Session ID of the declining agent</param>
    /// <param name="request">Decline details</param>
    /// <returns>Result with updated handoff</returns>
    Task<DeclineHandoffResult> DeclineHandoffAsync(
        string clientId,
        string handoffId,
        string decliningSessionId,
        DeclineHandoffRequest request);

    /// <summary>
    /// Get handoff history/chain for a specific task.
    /// </summary>
    /// <param name="clientId">Client ID scope</param>
    /// <param name="taskId">Task ID to get history for</param>
    /// <returns>Result with handoff chain and current owner</returns>
    Task<GetHandoffHistoryResult> GetHandoffHistoryAsync(string clientId, string taskId);

    /// <summary>
    /// Get pending handoffs for a specific agent session.
    /// Includes both targeted handoffs and open handoffs.
    /// </summary>
    /// <param name="clientId">Client ID scope</param>
    /// <param name="sessionId">Agent session ID</param>
    /// <returns>Result with list of claimable handoffs</returns>
    Task<ListHandoffsResult> GetPendingHandoffsForAgentAsync(string clientId, string sessionId);

    /// <summary>
    /// Process expired handoffs (background job).
    /// </summary>
    /// <param name="clientId">Client ID scope</param>
    /// <returns>Number of handoffs marked as expired</returns>
    Task<int> ProcessExpiredHandoffsAsync(string clientId);
}
