using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Models.Timeline;

namespace VibeSQL.Core.Services.Timeline;

/// <summary>
/// Service for managing timeline (x-vibe-type: timeline) operations.
/// </summary>
public class TimelineService : ITimelineService
{
    private readonly ITimelineRepository _timelineRepository;
    private readonly ILogger<TimelineService> _logger;

    // Vibe SQL uses client_id = 0 for system-level collections
    private const int SystemClientId = 0;

    public TimelineService(
        ITimelineRepository timelineRepository,
        ILogger<TimelineService> logger)
    {
        _timelineRepository = timelineRepository;
        _logger = logger;
    }

    public async Task<int> AppendEventAsync(
        string collection,
        string tableName,
        int documentId,
        string timelineField,
        TimelineEventRequest eventRequest)
    {
        if (string.IsNullOrWhiteSpace(collection))
        {
            throw new ArgumentException("Collection is required", nameof(collection));
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required", nameof(tableName));
        }

        if (documentId <= 0)
        {
            throw new ArgumentException("Document ID must be positive", nameof(documentId));
        }

        if (string.IsNullOrWhiteSpace(timelineField))
        {
            throw new ArgumentException("Timeline field is required", nameof(timelineField));
        }

        if (string.IsNullOrWhiteSpace(eventRequest.Event))
        {
            throw new ArgumentException("Event name is required", nameof(eventRequest));
        }

        try
        {
            var timelineEvent = new TimelineEvent
            {
                Timestamp = eventRequest.Timestamp,
                Event = eventRequest.Event,
                Data = eventRequest.Data
            };

            var index = await _timelineRepository.AppendEventAsync(
                SystemClientId,
                collection,
                tableName,
                documentId,
                timelineField,
                timelineEvent);

            _logger.LogInformation(
                "Appended event '{Event}' to timeline {Field} for document {DocumentId} at index {Index}",
                eventRequest.Event, timelineField, documentId, index);

            return index;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to append event to timeline {Field} for document {DocumentId}",
                timelineField, documentId);
            throw;
        }
    }

    public async Task<List<TimelineEvent>> GetTimelineAsync(
        string collection,
        string tableName,
        int documentId,
        string timelineField,
        TimelineFilter? filter = null)
    {
        if (string.IsNullOrWhiteSpace(collection))
        {
            throw new ArgumentException("Collection is required", nameof(collection));
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required", nameof(tableName));
        }

        if (documentId <= 0)
        {
            throw new ArgumentException("Document ID must be positive", nameof(documentId));
        }

        if (string.IsNullOrWhiteSpace(timelineField))
        {
            throw new ArgumentException("Timeline field is required", nameof(timelineField));
        }

        try
        {
            var events = await _timelineRepository.GetTimelineEventsAsync(
                SystemClientId,
                collection,
                tableName,
                documentId,
                timelineField,
                filter?.Since,
                filter?.Until,
                filter?.EventFilter,
                filter?.Limit);

            _logger.LogDebug(
                "Retrieved {Count} timeline events for document {DocumentId}",
                events.Count, documentId);

            return events;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve timeline for document {DocumentId}",
                documentId);
            throw;
        }
    }

    public async Task<TimelineSummary> GetSummaryAsync(
        string collection,
        string tableName,
        int documentId,
        string timelineField)
    {
        if (string.IsNullOrWhiteSpace(collection))
        {
            throw new ArgumentException("Collection is required", nameof(collection));
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required", nameof(tableName));
        }

        if (documentId <= 0)
        {
            throw new ArgumentException("Document ID must be positive", nameof(documentId));
        }

        if (string.IsNullOrWhiteSpace(timelineField))
        {
            throw new ArgumentException("Timeline field is required", nameof(timelineField));
        }

        try
        {
            var summary = await _timelineRepository.GetTimelineSummaryAsync(
                SystemClientId,
                collection,
                tableName,
                documentId,
                timelineField);

            _logger.LogDebug(
                "Generated timeline summary for document {DocumentId}: {EventCount} events",
                documentId, summary.EventCount);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to generate timeline summary for document {DocumentId}",
                documentId);
            throw;
        }
    }
}
