using VibeSQL.Core.DTOs.AgentMail;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for agent presence operations.
/// Handles business logic, validation, authorization, and rate limiting.
/// </summary>
public interface IAgentPresenceService
{
    /// <summary>
    /// Get presence for all agents, optionally filtered by status.
    /// </summary>
    Task<ListPresenceResult> GetAllPresenceAsync(
        string clientId,
        int userId,
        string? status = null,
        string? agentIds = null,
        int limit = 100,
        string? cursor = null);

    /// <summary>
    /// Get presence for a specific agent.
    /// </summary>
    Task<GetPresenceResult> GetPresenceAsync(
        string clientId,
        int userId,
        int agentId);

    /// <summary>
    /// Update presence for the authenticated agent.
    /// </summary>
    Task<UpdatePresenceResult> UpdatePresenceAsync(
        string clientId,
        int userId,
        int agentId,
        UpdatePresenceRequest request);

    /// <summary>
    /// Send heartbeat for an agent.
    /// </summary>
    Task<HeartbeatResult> SendHeartbeatAsync(
        string clientId,
        int userId,
        int agentId);

    /// <summary>
    /// Signal typing status in a thread.
    /// </summary>
    Task<TypingResult> SetTypingAsync(
        string clientId,
        int userId,
        int agentId,
        TypingRequest request);

    /// <summary>
    /// Mark stale agents as offline (for reaper service).
    /// </summary>
    Task<int> ReapStalePresenceAsync(string clientId, TimeSpan threshold);

    /// <summary>
    /// Get presence metrics (online/away counts).
    /// </summary>
    Task<(int online, int away)> GetPresenceMetricsAsync(string clientId);
}
