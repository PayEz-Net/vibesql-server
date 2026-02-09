using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for autonomy_settings table operations.
/// </summary>
public interface IAutonomyRepository
{
    /// <summary>
    /// Gets autonomy settings for a project.
    /// </summary>
    Task<AutonomySettings?> GetSettingsAsync(int projectId);

    /// <summary>
    /// Creates or updates autonomy settings.
    /// </summary>
    Task<AutonomySettings> UpsertSettingsAsync(AutonomySettings settings);

    /// <summary>
    /// Updates the enabled flag.
    /// </summary>
    Task UpdateEnabledAsync(int projectId, bool enabled);

    /// <summary>
    /// Updates the started_at timestamp.
    /// </summary>
    Task UpdateStartedAtAsync(int projectId, DateTime? startedAt);

    /// <summary>
    /// Updates the coordinator loop enabled flag.
    /// </summary>
    Task UpdateCoordinatorLoopEnabledAsync(int projectId, bool enabled);

    /// <summary>
    /// Updates the coordinator loop last run timestamp.
    /// </summary>
    Task UpdateCoordinatorLoopLastRunAtAsync(int projectId, DateTime lastRunAt);

    /// <summary>
    /// Gets all settings where autonomy and coordinator loop are enabled.
    /// </summary>
    Task<IEnumerable<AutonomySettings>> GetActiveCoordinatorLoopsAsync();
}
