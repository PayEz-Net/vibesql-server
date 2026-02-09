using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for timeline operations on Vibe SQL documents.
/// </summary>
public interface ITimelineRepository
{
    /// <summary>
    /// Appends an event to a JSONB timeline array.
    /// </summary>
    Task<int> AppendEventAsync(
        int clientId,
        string collection,
        string tableName,
        int documentId,
        string timelineField,
        TimelineEvent timelineEvent);

    /// <summary>
    /// Gets timeline events from a JSONB array.
    /// </summary>
    Task<List<TimelineEvent>> GetTimelineEventsAsync(
        int clientId,
        string collection,
        string tableName,
        int documentId,
        string timelineField,
        DateTime? since = null,
        DateTime? until = null,
        string? eventFilter = null,
        int? limit = null);

    /// <summary>
    /// Gets timeline summary aggregations.
    /// </summary>
    Task<TimelineSummary> GetTimelineSummaryAsync(
        int clientId,
        string collection,
        string tableName,
        int documentId,
        string timelineField);
}
