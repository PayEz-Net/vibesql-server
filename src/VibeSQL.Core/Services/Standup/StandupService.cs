using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Models.Standup;

namespace VibeSQL.Core.Services.Standup;

/// <summary>
/// Application service for managing agent standup entries.
/// Provides structured activity logging for supervised autonomy.
/// </summary>
public class StandupService : IStandupService
{
    private readonly IStandupRepository _standupRepository;
    private readonly ILogger<StandupService> _logger;

    // Valid event types from schema
    private static readonly string[] ValidEventTypes =
    {
        "started", "completed", "blocked", "review_requested",
        "review_passed", "review_failed", "milestone_done"
    };

    public StandupService(
        IStandupRepository standupRepository,
        ILogger<StandupService> logger)
    {
        _standupRepository = standupRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> RecordEntryAsync(StandupEntryRequest entry)
    {
        // Validate event type
        if (!ValidEventTypes.Contains(entry.EventType))
        {
            throw new ArgumentException($"Invalid event type: {entry.EventType}. " +
                                      $"Valid types: {string.Join(", ", ValidEventTypes)}");
        }

        // Validate required fields
        if (entry.ProjectId <= 0)
        {
            throw new ArgumentException("ProjectId is required");
        }

        if (entry.AgentId <= 0)
        {
            throw new ArgumentException("AgentId is required");
        }

        if (string.IsNullOrWhiteSpace(entry.Summary))
        {
            throw new ArgumentException("Summary is required");
        }

        if (entry.Summary.Length > 512)
        {
            throw new ArgumentException("Summary cannot exceed 512 characters");
        }

        try
        {
            var entryId = await _standupRepository.CreateEntryAsync(
                entry.ProjectId,
                entry.AgentId,
                entry.EventType,
                entry.TaskId,
                entry.Summary,
                entry.DetailsMd);

            _logger.LogInformation(
                "Recorded standup entry {EntryId} for agent {AgentId} in project {ProjectId}: {EventType}",
                entryId, entry.AgentId, entry.ProjectId, entry.EventType);

            return entryId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to record standup entry for agent {AgentId} in project {ProjectId}",
                entry.AgentId, entry.ProjectId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<StandupSummary> GetSummaryAsync(int projectId, DateTime since)
    {
        if (projectId <= 0)
        {
            throw new ArgumentException("ProjectId is required");
        }

        try
        {
            var until = DateTime.UtcNow;
            var entries = await _standupRepository.GetEntriesByProjectAsync(
                projectId,
                since,
                until,
                limit: null);

            // Calculate summary statistics
            var summary = new StandupSummary
            {
                ProjectId = projectId,
                Since = since,
                Until = until,
                TotalEntries = entries.Count,
                TasksStarted = entries.Count(e => e.EventType == "started"),
                TasksCompleted = entries.Count(e => e.EventType == "completed"),
                TasksBlocked = entries.Count(e => e.EventType == "blocked"),
                ReviewsRequested = entries.Count(e => e.EventType == "review_requested"),
                ReviewsPassed = entries.Count(e => e.EventType == "review_passed"),
                ReviewsFailed = entries.Count(e => e.EventType == "review_failed"),
                MilestonesDone = entries.Count(e => e.EventType == "milestone_done"),
                RecentEntries = entries.OrderByDescending(e => e.CreatedAt).Take(10).ToList()
            };

            _logger.LogDebug(
                "Generated summary for project {ProjectId}: {TotalEntries} entries since {Since}",
                projectId, summary.TotalEntries, since);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to generate standup summary for project {ProjectId}",
                projectId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<StandupEntry>> GetEntriesAsync(int projectId, StandupFilter? filter = null)
    {
        if (projectId <= 0)
        {
            throw new ArgumentException("ProjectId is required");
        }

        try
        {
            // Apply filters
            var agentId = filter?.AgentId;
            var eventType = filter?.EventType;
            var taskId = filter?.TaskId;
            var since = filter?.Since ?? DateTime.UtcNow.AddDays(-7);
            var until = filter?.Until ?? DateTime.UtcNow;
            var limit = filter?.Limit;

            // Validate event type if provided
            if (!string.IsNullOrEmpty(eventType) && !ValidEventTypes.Contains(eventType))
            {
                throw new ArgumentException($"Invalid event type: {eventType}");
            }

            var entries = await _standupRepository.GetEntriesByProjectAsync(
                projectId,
                since,
                until,
                agentId,
                eventType,
                taskId,
                limit);

            _logger.LogDebug(
                "Retrieved {Count} standup entries for project {ProjectId}",
                entries.Count, projectId);

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve standup entries for project {ProjectId}",
                projectId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<StandupEntry>> GetRecentEntriesAsync(int projectId, int hours = 2)
    {
        if (projectId <= 0)
        {
            throw new ArgumentException("ProjectId is required");
        }

        if (hours <= 0)
        {
            throw new ArgumentException("Hours must be greater than 0");
        }

        try
        {
            var since = DateTime.UtcNow.AddHours(-hours);
            var until = DateTime.UtcNow;

            var entries = await _standupRepository.GetEntriesByProjectAsync(
                projectId,
                since,
                until,
                limit: null);

            _logger.LogDebug(
                "Retrieved {Count} recent standup entries for project {ProjectId} (last {Hours} hours)",
                entries.Count, projectId, hours);

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve recent standup entries for project {ProjectId}",
                projectId);
            throw;
        }
    }
}
