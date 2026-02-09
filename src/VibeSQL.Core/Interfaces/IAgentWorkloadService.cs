using VibeSQL.Core.DTOs.AgentMail;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for calculating and retrieving agent workload metrics.
/// Implements the workload scoring algorithm per spec.
/// </summary>
public interface IAgentWorkloadService
{
    /// <summary>
    /// Get workload metrics for a specific agent.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="userId">User ID for authorization (0 for admin mode)</param>
    /// <param name="agentName">Agent name to get workload for</param>
    /// <param name="query">Query parameters (period, include_history, include_factors)</param>
    /// <returns>Workload data with score, status, and metrics</returns>
    Task<AgentWorkloadResult> GetAgentWorkloadAsync(
        string clientId,
        int userId,
        string agentName,
        AgentWorkloadQuery query);

    /// <summary>
    /// Get workload summary for all agents.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="userId">User ID for authorization (0 for admin mode)</param>
    /// <param name="query">Query parameters (period)</param>
    /// <returns>Summary with team stats and brief metrics per agent</returns>
    Task<AgentWorkloadSummaryResult> GetAllAgentWorkloadsAsync(
        string clientId,
        int userId,
        AgentWorkloadQuery query);
}
