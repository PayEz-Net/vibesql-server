using VibeSQL.Core.Entities;
using VibeSQL.Core.Models.Standup;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for managing agent standup entries for supervised autonomy.
/// </summary>
public interface IStandupService
{
    /// <summary>
    /// Records a new standup entry for an agent.
    /// </summary>
    /// <param name="entry">The standup entry to record</param>
    /// <returns>The created entry ID</returns>
    Task<int> RecordEntryAsync(StandupEntryRequest entry);

    /// <summary>
    /// Retrieves a summary of standup entries for a project since a given time.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="since">Start time for summary</param>
    /// <returns>Summary of standup activity</returns>
    Task<StandupSummary> GetSummaryAsync(int projectId, DateTime since);

    /// <summary>
    /// Retrieves standup entries filtered by criteria.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="filter">Optional filter criteria</param>
    /// <returns>List of standup entries</returns>
    Task<List<StandupEntry>> GetEntriesAsync(int projectId, StandupFilter? filter = null);

    /// <summary>
    /// Retrieves recent standup entries within the specified time window.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="hours">Number of hours to look back</param>
    /// <returns>List of recent standup entries</returns>
    Task<List<StandupEntry>> GetRecentEntriesAsync(int projectId, int hours = 2);
}
