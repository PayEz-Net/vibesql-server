using VibeSQL.Core.Entities;
using VibeSQL.Core.Models.Timeline;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for managing timeline (x-vibe-type: timeline) operations on JSONB arrays.
/// </summary>
public interface ITimelineService
{
    /// <summary>
    /// Appends an event to a timeline field.
    /// </summary>
    /// <param name="collection">Collection name</param>
    /// <param name="tableName">Table name</param>
    /// <param name="documentId">Document ID</param>
    /// <param name="timelineField">Timeline field name (e.g., "timeline")</param>
    /// <param name="eventRequest">Event to append</param>
    /// <returns>Index of appended event</returns>
    Task<int> AppendEventAsync(
        string collection,
        string tableName,
        int documentId,
        string timelineField,
        TimelineEventRequest eventRequest);

    /// <summary>
    /// Gets timeline events with optional filtering.
    /// </summary>
    /// <param name="collection">Collection name</param>
    /// <param name="tableName">Table name</param>
    /// <param name="documentId">Document ID</param>
    /// <param name="timelineField">Timeline field name</param>
    /// <param name="filter">Optional filter criteria</param>
    /// <returns>List of timeline events</returns>
    Task<List<TimelineEvent>> GetTimelineAsync(
        string collection,
        string tableName,
        int documentId,
        string timelineField,
        TimelineFilter? filter = null);

    /// <summary>
    /// Gets a summary of timeline activity.
    /// </summary>
    /// <param name="collection">Collection name</param>
    /// <param name="tableName">Table name</param>
    /// <param name="documentId">Document ID</param>
    /// <param name="timelineField">Timeline field name</param>
    /// <returns>Timeline summary with aggregations</returns>
    Task<TimelineSummary> GetSummaryAsync(
        string collection,
        string tableName,
        int documentId,
        string timelineField);
}
