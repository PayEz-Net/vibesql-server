using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for timeline operations on Vibe SQL JSONB arrays.
/// </summary>
public class TimelineRepository : ITimelineRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<TimelineRepository> _logger;

    public TimelineRepository(VibeDbContext context, ILogger<TimelineRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> AppendEventAsync(
        int clientId,
        string collection,
        string tableName,
        int documentId,
        string timelineField,
        TimelineEvent timelineEvent)
    {
        // Find the document
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == collection
                     && d.TableName == tableName
                     && d.DeletedAt == null)
            .ToListAsync();

        var document = documents.FirstOrDefault(d =>
        {
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(d.Data);
                // Match on the primary key field (usually "id" or "{table}_id")
                if (data != null && data.ContainsKey("id"))
                {
                    return data["id"].GetInt32() == documentId;
                }
                // Try document_id for agent_documents
                if (data != null && data.ContainsKey("document_id"))
                {
                    return data["document_id"].GetInt32() == documentId;
                }
                // Try other common ID fields
                var idField = $"{tableName}_id";
                if (data != null && data.ContainsKey(idField))
                {
                    return data[idField].GetInt32() == documentId;
                }
            }
            catch { }
            return false;
        });

        if (document == null)
        {
            throw new InvalidOperationException($"Document {documentId} not found in {collection}.{tableName}");
        }

        // Deserialize the data
        var docData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(document.Data);
        if (docData == null)
        {
            throw new InvalidOperationException("Failed to deserialize document data");
        }

        // Get or create timeline array
        List<TimelineEvent> timeline;
        if (docData.ContainsKey(timelineField) && docData[timelineField].ValueKind == JsonValueKind.Array)
        {
            timeline = JsonSerializer.Deserialize<List<TimelineEvent>>(docData[timelineField].GetRawText()) ?? new List<TimelineEvent>();
        }
        else
        {
            timeline = new List<TimelineEvent>();
        }

        // Append the new event
        timeline.Add(timelineEvent);

        // Sort by timestamp to maintain order
        timeline = timeline.OrderBy(e => e.Timestamp).ToList();

        // Update the data
        docData[timelineField] = JsonSerializer.SerializeToElement(timeline);

        // Serialize back
        document.Data = JsonSerializer.Serialize(docData);
        document.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Appended event to timeline {Field} for document {DocumentId} in {Collection}.{Table}",
            timelineField, documentId, collection, tableName);

        return timeline.Count - 1; // Return index of appended event
    }

    public async Task<List<TimelineEvent>> GetTimelineEventsAsync(
        int clientId,
        string collection,
        string tableName,
        int documentId,
        string timelineField,
        DateTime? since = null,
        DateTime? until = null,
        string? eventFilter = null,
        int? limit = null)
    {
        // Find the document
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == collection
                     && d.TableName == tableName
                     && d.DeletedAt == null)
            .ToListAsync();

        var document = documents.FirstOrDefault(d =>
        {
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(d.Data);
                if (data != null && data.ContainsKey("id"))
                {
                    return data["id"].GetInt32() == documentId;
                }
                if (data != null && data.ContainsKey("document_id"))
                {
                    return data["document_id"].GetInt32() == documentId;
                }
                var idField = $"{tableName}_id";
                if (data != null && data.ContainsKey(idField))
                {
                    return data[idField].GetInt32() == documentId;
                }
            }
            catch { }
            return false;
        });

        if (document == null)
        {
            return new List<TimelineEvent>();
        }

        // Deserialize the data
        var docData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(document.Data);
        if (docData == null || !docData.ContainsKey(timelineField))
        {
            return new List<TimelineEvent>();
        }

        // Get timeline array
        var timeline = JsonSerializer.Deserialize<List<TimelineEvent>>(docData[timelineField].GetRawText()) ?? new List<TimelineEvent>();

        // Apply filters
        var filtered = timeline.AsEnumerable();

        if (since.HasValue)
        {
            filtered = filtered.Where(e => e.Timestamp >= since.Value);
        }

        if (until.HasValue)
        {
            filtered = filtered.Where(e => e.Timestamp <= until.Value);
        }

        if (!string.IsNullOrEmpty(eventFilter))
        {
            filtered = filtered.Where(e => e.Event == eventFilter);
        }

        // Sort by timestamp descending (most recent first)
        filtered = filtered.OrderByDescending(e => e.Timestamp);

        // Apply limit
        if (limit.HasValue && limit.Value > 0)
        {
            filtered = filtered.Take(limit.Value);
        }

        return filtered.ToList();
    }

    public async Task<TimelineSummary> GetTimelineSummaryAsync(
        int clientId,
        string collection,
        string tableName,
        int documentId,
        string timelineField)
    {
        // Get all events
        var events = await GetTimelineEventsAsync(
            clientId,
            collection,
            tableName,
            documentId,
            timelineField);

        if (!events.Any())
        {
            return new TimelineSummary
            {
                EventCount = 0,
                RecentEvents = new List<TimelineEvent>()
            };
        }

        var first = events.Min(e => e.Timestamp);
        var last = events.Max(e => e.Timestamp);
        var duration = (last - first).TotalDays;

        return new TimelineSummary
        {
            FirstEvent = first,
            LastEvent = last,
            EventCount = events.Count,
            DurationDays = duration,
            RecentEvents = events.OrderByDescending(e => e.Timestamp).Take(5).ToList()
        };
    }
}
