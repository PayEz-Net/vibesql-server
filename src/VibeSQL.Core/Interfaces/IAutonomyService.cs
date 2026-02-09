using VibeSQL.Core.Models.Autonomy;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for managing supervised autonomous operation of agents.
/// </summary>
public interface IAutonomyService
{
    /// <summary>
    /// Starts autonomy mode for a project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="specId">Spec document ID</param>
    /// <param name="milestone">Target milestone</param>
    /// <returns>True if started successfully</returns>
    Task<bool> StartAutonomyAsync(int projectId, int specId, string milestone);

    /// <summary>
    /// Stops autonomy mode for a project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="reason">Reason for stopping</param>
    Task StopAutonomyAsync(int projectId, string reason);

    /// <summary>
    /// Gets the current autonomy status for a project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <returns>Current autonomy status</returns>
    Task<AutonomyStatus> GetStatusAsync(int projectId);

    /// <summary>
    /// Checks stop conditions and triggers stop if needed.
    /// Should be called after task completion.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    Task CheckStopConditionsAsync(int projectId);
}
