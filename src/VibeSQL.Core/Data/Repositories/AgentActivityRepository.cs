using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for agent_activity_log table operations.
/// Handles storage and retrieval of agent activity events following Clean Architecture.
/// </summary>
public class AgentActivityRepository : IAgentActivityRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<AgentActivityRepository> _logger;

    private const string CollectionName = "agent_mail";
    private const string ActivitiesTable = "agent_activity_log";

    public AgentActivityRepository(VibeDbContext context, ILogger<AgentActivityRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VibeDocument> LogActivityAsync(
        int clientId,
        string agentId,
        string activityType,
        string? targetType,
        string? targetId,
        string? metadataJson)
    {
        var now = DateTimeOffset.UtcNow;
        var activityId = Guid.NewGuid().ToString();

        var activityData = new Dictionary<string, object?>
        {
            ["id"] = activityId,
            ["agent_id"] = agentId,
            ["activity_type"] = activityType,
            ["target_type"] = targetType,
            ["target_id"] = targetId,
            ["metadata"] = metadataJson,
            ["created_at"] = now.ToString("o")
        };

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = 0,
            Collection = CollectionName,
            TableName = ActivitiesTable,
            Data = JsonSerializer.Serialize(activityData),
            CreatedAt = now,
            CreatedBy = 0
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogDebug(
            "AGENT_ACTIVITY_LOGGED: ActivityId={ActivityId}, AgentId={AgentId}, Type={Type}",
            activityId, agentId, activityType);

        return document;
    }

    public async Task<(List<VibeDocument> Activities, bool HasMore)> GetActivitiesAsync(
        int clientId,
        string? agentId = null,
        string? activityType = null,
        List<string>? activityTypes = null,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null,
        int limit = 50,
        string? cursor = null)
    {
        var activities = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == ActivitiesTable
                     && d.DeletedAt == null)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        // Apply filters
        var filtered = activities.Where(d =>
        {
            var data = TryDeserializeActivity(d.Data);
            if (data == null) return false;

            // Filter by agent
            if (!string.IsNullOrEmpty(agentId) && 
                !string.Equals(data.AgentId, agentId, StringComparison.OrdinalIgnoreCase))
                return false;

            // Filter by single type
            if (!string.IsNullOrEmpty(activityType) && 
                !string.Equals(data.ActivityType, activityType, StringComparison.OrdinalIgnoreCase))
                return false;

            // Filter by multiple types
            if (activityTypes != null && activityTypes.Count > 0 &&
                !activityTypes.Contains(data.ActivityType, StringComparer.OrdinalIgnoreCase))
                return false;

            // Filter by time range
            if (since.HasValue && data.CreatedAt < since.Value)
                return false;
            if (until.HasValue && data.CreatedAt > until.Value)
                return false;

            // Apply cursor (skip items before cursor position)
            if (!string.IsNullOrEmpty(cursor))
            {
                var cursorData = DecodeCursor(cursor);
                if (cursorData != null && !string.IsNullOrEmpty(cursorData.LastId))
                {
                    // Cursor-based pagination: skip until we pass the cursor
                    if (string.Equals(data.Id, cursorData.LastId, StringComparison.Ordinal))
                        return false;
                }
            }

            return true;
        });

        var result = filtered.Take(limit + 1).ToList();
        var hasMore = result.Count > limit;

        return (result.Take(limit).ToList(), hasMore);
    }

    public async Task<List<VibeDocument>> GetAggregatedActivitiesAsync(
        int clientId,
        string? agentId = null,
        string? activityType = null,
        List<string>? activityTypes = null,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null,
        int limit = 50,
        string? cursor = null)
    {
        // Get raw activities first
        var (activities, _) = await GetActivitiesAsync(
            clientId, agentId, activityType, activityTypes,
            since ?? DateTimeOffset.UtcNow.AddHours(-24),
            until, limit * 5, cursor); // Fetch more to allow for aggregation

        // Parse all activities
        var parsed = activities
            .Select(d => new
            {
                Document = d,
                Data = TryDeserializeActivity(d.Data)
            })
            .Where(x => x.Data != null)
            .Select(x => new
            {
                x.Document,
                Data = x.Data!
            })
            .OrderByDescending(x => x.Data.CreatedAt)
            .ToList();

        // Apply aggregation rules
        var aggregated = new List<VibeDocument>();
        var processed = new HashSet<string>();

        foreach (var item in parsed)
        {
            if (processed.Contains(item.Data.Id))
                continue;

            // Find items in the same aggregation group
            var rule = GetAggregationRule(item.Data.ActivityType);
            if (rule != null)
            {
                var windowStart = item.Data.CreatedAt.AddSeconds(-rule.WindowSeconds);
                var group = parsed
                    .Where(p => !processed.Contains(p.Data.Id)
                             && string.Equals(p.Data.AgentId, item.Data.AgentId, StringComparison.OrdinalIgnoreCase)
                             && string.Equals(p.Data.ActivityType, item.Data.ActivityType, StringComparison.OrdinalIgnoreCase)
                             && p.Data.CreatedAt >= windowStart
                             && p.Data.CreatedAt <= item.Data.CreatedAt)
                    .ToList();

                if (group.Count >= rule.MinCount)
                {
                    // Create aggregated document
                    foreach (var g in group)
                        processed.Add(g.Data.Id);

                    var aggDoc = CreateAggregatedDocument(item.Document, group.Cast<object>().ToList());
                    aggregated.Add(aggDoc);
                    continue;
                }
            }

            // Not aggregated - add as single item
            processed.Add(item.Data.Id);
            aggregated.Add(item.Document);

            if (aggregated.Count >= limit)
                break;
        }

        return aggregated.Take(limit).ToList();
    }

    public async Task<VibeDocument?> GetActivityByIdAsync(int clientId, string activityId)
    {
        var activities = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == ActivitiesTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return activities.FirstOrDefault(d =>
        {
            var data = TryDeserializeActivity(d.Data);
            return data != null && string.Equals(data.Id, activityId, StringComparison.Ordinal);
        });
    }

    public async Task<int> DeleteOldActivitiesAsync(int clientId, TimeSpan retentionPeriod)
    {
        var cutoff = DateTimeOffset.UtcNow - retentionPeriod;

        var oldActivities = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == ActivitiesTable
                     && d.DeletedAt == null
                     && d.CreatedAt < cutoff)
            .ToListAsync();

        foreach (var doc in oldActivities)
        {
            doc.DeletedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "AGENT_ACTIVITY_CLEANUP: ClientId={ClientId}, Deleted={Count}, Cutoff={Cutoff}",
            clientId, oldActivities.Count, cutoff);

        return oldActivities.Count;
    }

    public async Task<int> CountActivitiesAsync(
        int clientId,
        string agentId,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null)
    {
        var activities = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == ActivitiesTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return activities.Count(d =>
        {
            var data = TryDeserializeActivity(d.Data);
            if (data == null) return false;

            if (!string.Equals(data.AgentId, agentId, StringComparison.OrdinalIgnoreCase))
                return false;

            if (since.HasValue && data.CreatedAt < since.Value)
                return false;
            if (until.HasValue && data.CreatedAt > until.Value)
                return false;

            return true;
        });
    }

    #region Private Methods

    private ActivityData? TryDeserializeActivity(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new ActivityData
            {
                Id = GetString(root, "id") ?? "",
                AgentId = GetString(root, "agent_id") ?? "",
                ActivityType = GetString(root, "activity_type") ?? "",
                TargetType = GetString(root, "target_type"),
                TargetId = GetString(root, "target_id"),
                MetadataJson = GetString(root, "metadata"),
                CreatedAt = GetDateTimeOffset(root, "created_at") ?? DateTimeOffset.UtcNow
            };
        }
        catch
        {
            return null;
        }
    }

    private string? GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private DateTimeOffset? GetDateTimeOffset(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var value))
        {
            if (value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var dto))
                return dto;
        }
        return null;
    }

    private CursorData? DecodeCursor(string cursor)
    {
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return JsonSerializer.Deserialize<CursorData>(json);
        }
        catch
        {
            return null;
        }
    }

    private AggregationRule? GetAggregationRule(string activityType)
    {
        return activityType switch
        {
            "message_sent" => new AggregationRule { WindowSeconds = 300, MinCount = 2 },
            "tool_invoked" => new AggregationRule { WindowSeconds = 60, MinCount = 3 },
            "file_written" => new AggregationRule { WindowSeconds = 120, MinCount = 3 },
            _ => null
        };
    }

    private VibeDocument CreateAggregatedDocument(VibeDocument representative, List<object> group)
    {
        // Extract activity data from the group using reflection
        var groupData = group.Select(g =>
        {
            var docProp = g.GetType().GetProperty("Document");
            var dataProp = g.GetType().GetProperty("Data");
            return new
            {
                Document = docProp?.GetValue(g) as VibeDocument,
                Data = dataProp?.GetValue(g) as ActivityData
            };
        }).Where(x => x.Data != null && x.Document != null).ToList();

        var firstItem = groupData.OrderBy(g => g.Data!.CreatedAt).First();
        var lastItem = groupData.OrderByDescending(g => g.Data!.CreatedAt).First();

        var aggData = new Dictionary<string, object?>
        {
            ["id"] = representative.DocumentId.ToString(),
            ["agent_id"] = firstItem.Data!.AgentId,
            ["activity_type"] = firstItem.Data.ActivityType,
            ["target_type"] = firstItem.Data.TargetType,
            ["created_at"] = lastItem.Data!.CreatedAt.ToString("o"),
            ["group_count"] = group.Count,
            ["first_at"] = firstItem.Data.CreatedAt.ToString("o"),
            ["last_at"] = lastItem.Data.CreatedAt.ToString("o"),
            ["item_ids"] = string.Join(",", groupData.Select(g => g.Data!.Id))
        };

        return new VibeDocument
        {
            DocumentId = representative.DocumentId,
            ClientId = representative.ClientId,
            Collection = representative.Collection,
            TableName = representative.TableName,
            Data = JsonSerializer.Serialize(aggData),
            CreatedAt = lastItem.Data.CreatedAt
        };
    }

    #endregion

    #region Internal Types

    private class ActivityData
    {
        public string Id { get; set; } = "";
        public string AgentId { get; set; } = "";
        public string ActivityType { get; set; } = "";
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public string? MetadataJson { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    private class CursorData
    {
        public string? LastId { get; set; }
    }

    private class AggregationRule
    {
        public int WindowSeconds { get; set; }
        public int MinCount { get; set; }
    }

    #endregion
}
