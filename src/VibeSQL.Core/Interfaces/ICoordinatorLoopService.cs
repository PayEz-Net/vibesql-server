using VibeSQL.Core.Models.CoordinatorLoop;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for managing the coordinator loop - automated agent orchestration.
/// The coordinator loop runs when autonomy is enabled AND coordinator_loop_enabled is true.
/// It finds the designated coordinator agent (is_coordinator=true) and executes actions as that agent.
/// </summary>
public interface ICoordinatorLoopService
{
    /// <summary>
    /// Starts the coordinator loop for a project.
    /// Requires autonomy to be enabled and a coordinator agent to be designated.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <returns>True if started successfully</returns>
    Task<bool> StartLoopAsync(int projectId);

    /// <summary>
    /// Stops the coordinator loop for a project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="reason">Reason for stopping</param>
    Task StopLoopAsync(int projectId, string reason);

    /// <summary>
    /// Gets the current coordinator loop status for a project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <returns>Current coordinator loop status</returns>
    Task<CoordinatorLoopStatus> GetStatusAsync(int projectId);

    /// <summary>
    /// Executes a single iteration of the coordinator loop.
    /// Called by the background service at the configured interval.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <returns>Result of the loop iteration</returns>
    Task<CoordinatorLoopResult> ExecuteIterationAsync(int projectId);

    /// <summary>
    /// Gets all projects with coordinator loop enabled for background processing.
    /// </summary>
    /// <returns>List of project IDs with coordinator loop enabled</returns>
    Task<IEnumerable<int>> GetActiveLoopProjectsAsync();
}
