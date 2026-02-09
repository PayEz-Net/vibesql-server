using VibeSQL.Core.Models;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for agent profile operations.
/// Loads agent data from Vibe JSON Schema collections.
/// </summary>
public interface IAgentProfileRepository
{
    /// <summary>
    /// Get agent profile by ID from agent_profiles table
    /// </summary>
    Task<AgentProfile?> GetAgentProfileAsync(int agentProfileId);

    /// <summary>
    /// Get agent capabilities from agent_capabilities table
    /// </summary>
    Task<IEnumerable<string>> GetAgentCapabilitiesAsync(int agentProfileId);

    /// <summary>
    /// Get agent team from agent_teams table
    /// </summary>
    Task<AgentTeam?> GetAgentTeamAsync(int agentProfileId);

    /// <summary>
    /// Check if agent is active
    /// </summary>
    Task<bool> IsAgentActiveAsync(int agentProfileId);

    /// <summary>
    /// Get the coordinator agent for a project (is_coordinator = true)
    /// </summary>
    Task<AgentProfile?> GetCoordinatorAgentAsync(int projectId);

    /// <summary>
    /// Get all active agents for a project
    /// </summary>
    Task<IEnumerable<AgentProfile>> GetProjectAgentsAsync(int projectId, bool activeOnly = true);

    /// <summary>
    /// Get agents that have been idle for longer than the threshold
    /// </summary>
    Task<IEnumerable<AgentProfile>> GetIdleAgentsAsync(int projectId, TimeSpan idleThreshold);
}
