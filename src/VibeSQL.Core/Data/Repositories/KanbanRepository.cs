using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for kanban_tasks table operations.
/// </summary>
public class KanbanRepository : IKanbanRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<KanbanRepository> _logger;

    private const string CollectionName = "vibe_agents";
    private const string TableName = "kanban_tasks";

    public KanbanRepository(VibeDbContext context, ILogger<KanbanRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> CountTasksAsync(
        int projectId,
        int? specId = null,
        string? milestone = null,
        string? status = null,
        bool excludeStatus = false)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == 0
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        var tasks = documents
            .Select(d => ParseTask(d))
            .Where(t => t != null)
            .Cast<KanbanTaskInternal>()
            .ToList();

        // Filter by project_id
        var filtered = tasks.Where(t => t.ProjectId == projectId);

        // Filter by spec_id if provided
        if (specId.HasValue)
        {
            filtered = filtered.Where(t => t.SpecId == specId.Value);
        }

        // Filter by milestone if provided
        if (!string.IsNullOrEmpty(milestone))
        {
            filtered = filtered.Where(t => t.Milestone == milestone);
        }

        // Filter by status if provided
        if (!string.IsNullOrEmpty(status))
        {
            if (excludeStatus)
            {
                filtered = filtered.Where(t => t.Status != status);
            }
            else
            {
                filtered = filtered.Where(t => t.Status == status);
            }
        }

        return filtered.Count();
    }

    public async Task<IEnumerable<KanbanTask>> GetStaleReviewTasksAsync(int projectId, TimeSpan reviewThreshold)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == 0
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        var cutoffTime = DateTime.UtcNow - reviewThreshold;
        var staleTasks = new List<KanbanTask>();

        foreach (var document in documents)
        {
            var task = ParseFullTask(document);
            if (task != null
                && task.Status == "review"
                && task.UpdatedAt.HasValue
                && task.UpdatedAt.Value < cutoffTime)
            {
                staleTasks.Add(task);
            }
        }

        return staleTasks;
    }

    public async Task<IEnumerable<KanbanTask>> GetBlockedTasksAsync(int projectId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == 0
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        var blockedTasks = new List<KanbanTask>();

        foreach (var document in documents)
        {
            var task = ParseFullTask(document);
            if (task != null && task.Status == "blocked")
            {
                blockedTasks.Add(task);
            }
        }

        return blockedTasks;
    }

    public async Task<IEnumerable<KanbanTask>> GetInProgressTasksAsync(int projectId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == 0
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        var inProgressTasks = new List<KanbanTask>();

        foreach (var document in documents)
        {
            var task = ParseFullTask(document);
            if (task != null && task.Status == "in_progress")
            {
                inProgressTasks.Add(task);
            }
        }

        return inProgressTasks;
    }

    private KanbanTaskInternal? ParseTask(VibeDocument document)
    {
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(document.Data);
            if (data == null) return null;

            return new KanbanTaskInternal
            {
                ProjectId = data["project_id"].GetInt32(),
                SpecId = data.TryGetValue("spec_id", out var specId) && specId.ValueKind != JsonValueKind.Null
                    ? specId.GetInt32()
                    : null,
                Milestone = data.TryGetValue("milestone", out var milestone) && milestone.ValueKind != JsonValueKind.Null
                    ? milestone.GetString()
                    : null,
                Status = data["status"].GetString() ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse kanban task from document {DocumentId}", document.DocumentId);
            return null;
        }
    }

    private KanbanTask? ParseFullTask(VibeDocument document)
    {
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(document.Data);
            if (data == null) return null;

            return new KanbanTask
            {
                TaskId = data.TryGetValue("task_id", out var taskId) && taskId.ValueKind != JsonValueKind.Null
                    ? taskId.GetInt32() : 0,
                Title = data.TryGetValue("title", out var title) && title.ValueKind != JsonValueKind.Null
                    ? title.GetString() ?? string.Empty : string.Empty,
                Description = data.TryGetValue("description", out var desc) && desc.ValueKind != JsonValueKind.Null
                    ? desc.GetString() : null,
                Status = data.TryGetValue("status", out var status) && status.ValueKind != JsonValueKind.Null
                    ? status.GetString() ?? "backlog" : "backlog",
                Priority = data.TryGetValue("priority", out var priority) && priority.ValueKind != JsonValueKind.Null
                    ? priority.GetString() ?? "normal" : "normal",
                AssignedTo = data.TryGetValue("assigned_to", out var assignedTo) && assignedTo.ValueKind != JsonValueKind.Null
                    ? assignedTo.GetString() : null,
                Blockers = data.TryGetValue("blockers", out var blockers) && blockers.ValueKind != JsonValueKind.Null
                    ? blockers.GetString() : null,
                CreatedAt = data.TryGetValue("created_at", out var createdAt) && createdAt.ValueKind != JsonValueKind.Null
                    ? DateTime.Parse(createdAt.GetString() ?? DateTime.UtcNow.ToString()) : DateTime.UtcNow,
                UpdatedAt = data.TryGetValue("updated_at", out var updatedAt) && updatedAt.ValueKind != JsonValueKind.Null
                    ? DateTime.Parse(updatedAt.GetString() ?? DateTime.UtcNow.ToString()) : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse full kanban task from document {DocumentId}", document.DocumentId);
            return null;
        }
    }

    private class KanbanTaskInternal
    {
        public int ProjectId { get; set; }
        public int? SpecId { get; set; }
        public string? Milestone { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
