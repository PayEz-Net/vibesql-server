using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for standup_entries table operations.
/// Abstracts data access for agent standup logging following Clean Architecture.
/// </summary>
public class StandupRepository : IStandupRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<StandupRepository> _logger;

    private const string CollectionName = "vibe_agents";
    private const string TableName = "standup_entries";

    public StandupRepository(VibeDbContext context, ILogger<StandupRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> CreateEntryAsync(
        int projectId,
        int agentId,
        string eventType,
        int? taskId,
        string summary,
        string? detailsMd)
    {
        var now = DateTimeOffset.UtcNow;

        // Get next ID from sequence
        var nextId = await GetNextEntryIdAsync();

        var entryData = new
        {
            entry_id = nextId,
            project_id = projectId,
            agent_id = agentId,
            event_type = eventType,
            task_id = taskId,
            summary = summary,
            details_md = detailsMd,
            created_at = now
        };

        var document = new VibeSQL.Core.Entities.VibeDocument
        {
            ClientId = 0, // System-level for vibe_agents
            Collection = CollectionName,
            TableName = TableName,
            Data = JsonSerializer.Serialize(entryData),
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created standup entry {EntryId} for agent {AgentId} in project {ProjectId}",
            nextId, agentId, projectId);

        return nextId;
    }

    public async Task<List<StandupEntry>> GetEntriesByProjectAsync(
        int projectId,
        DateTime since,
        DateTime until,
        int? agentId = null,
        string? eventType = null,
        int? taskId = null,
        int? limit = null)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == 0
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        var entries = documents
            .Select(d => ParseEntry(d))
            .Where(e => e != null)
            .Cast<StandupEntry>()
            .Where(e => e.ProjectId == projectId
                     && e.CreatedAt >= since
                     && e.CreatedAt <= until)
            .ToList();

        // Apply optional filters
        if (agentId.HasValue)
        {
            entries = entries.Where(e => e.AgentId == agentId.Value).ToList();
        }

        if (!string.IsNullOrEmpty(eventType))
        {
            entries = entries.Where(e => e.EventType == eventType).ToList();
        }

        if (taskId.HasValue)
        {
            entries = entries.Where(e => e.TaskId == taskId.Value).ToList();
        }

        // Order by created_at descending (most recent first)
        entries = entries.OrderByDescending(e => e.CreatedAt).ToList();

        // Apply limit if specified
        if (limit.HasValue && limit.Value > 0)
        {
            entries = entries.Take(limit.Value).ToList();
        }

        return entries;
    }

    public async Task<StandupEntry?> GetEntryByIdAsync(int entryId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == 0
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        return documents
            .Select(d => ParseEntry(d))
            .FirstOrDefault(e => e?.EntryId == entryId);
    }

    private StandupEntry? ParseEntry(VibeSQL.Core.Entities.VibeDocument document)
    {
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(document.Data);
            if (data == null) return null;

            return new StandupEntry
            {
                EntryId = data["entry_id"].GetInt32(),
                ProjectId = data["project_id"].GetInt32(),
                AgentId = data["agent_id"].GetInt32(),
                EventType = data["event_type"].GetString() ?? string.Empty,
                TaskId = data.TryGetValue("task_id", out var taskId) && taskId.ValueKind != JsonValueKind.Null
                    ? taskId.GetInt32()
                    : null,
                Summary = data["summary"].GetString() ?? string.Empty,
                DetailsMd = data.TryGetValue("details_md", out var detailsMd) && detailsMd.ValueKind != JsonValueKind.Null
                    ? detailsMd.GetString()
                    : null,
                CreatedAt = data["created_at"].GetDateTimeOffset().DateTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse standup entry from document {DocumentId}", document.DocumentId);
            return null;
        }
    }

    private async Task<int> GetNextEntryIdAsync()
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == 0
                     && d.Collection == CollectionName
                     && d.TableName == TableName)
            .ToListAsync();

        var maxId = documents
            .Select(d =>
            {
                try
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(d.Data);
                    return data?["entry_id"].GetInt32() ?? 0;
                }
                catch
                {
                    return 0;
                }
            })
            .DefaultIfEmpty(0)
            .Max();

        return maxId + 1;
    }
}
