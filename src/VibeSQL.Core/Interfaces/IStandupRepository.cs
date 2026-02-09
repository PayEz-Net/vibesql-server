using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for standup_entries table operations.
/// Abstracts data access for agent standup logging.
/// </summary>
public interface IStandupRepository
{
    /// <summary>
    /// Creates a new standup entry.
    /// </summary>
    Task<int> CreateEntryAsync(
        int projectId,
        int agentId,
        string eventType,
        int? taskId,
        string summary,
        string? detailsMd);

    /// <summary>
    /// Retrieves standup entries for a project within a time range.
    /// </summary>
    Task<List<StandupEntry>> GetEntriesByProjectAsync(
        int projectId,
        DateTime since,
        DateTime until,
        int? agentId = null,
        string? eventType = null,
        int? taskId = null,
        int? limit = null);

    /// <summary>
    /// Retrieves a single standup entry by ID.
    /// </summary>
    Task<StandupEntry?> GetEntryByIdAsync(int entryId);
}
