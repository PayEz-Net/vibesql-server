using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for kanban_tasks table operations.
/// </summary>
public interface IKanbanRepository
{
    /// <summary>
    /// Counts tasks matching the filter criteria.
    /// </summary>
    Task<int> CountTasksAsync(
        int projectId,
        int? specId = null,
        string? milestone = null,
        string? status = null,
        bool excludeStatus = false);

    /// <summary>
    /// Gets tasks in review status that have been pending longer than the threshold.
    /// </summary>
    Task<IEnumerable<KanbanTask>> GetStaleReviewTasksAsync(int projectId, TimeSpan reviewThreshold);

    /// <summary>
    /// Gets blocked tasks for a project.
    /// </summary>
    Task<IEnumerable<KanbanTask>> GetBlockedTasksAsync(int projectId);

    /// <summary>
    /// Gets tasks currently in progress for a project.
    /// </summary>
    Task<IEnumerable<KanbanTask>> GetInProgressTasksAsync(int projectId);
}
