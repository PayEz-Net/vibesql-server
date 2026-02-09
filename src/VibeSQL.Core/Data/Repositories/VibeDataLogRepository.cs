using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for Vibe data log operations.
/// Uses the Vibe document system (vibe.documents with collection='vibe_app', table='data_logs').
/// </summary>
public class VibeDataLogRepository : IVibeDataLogRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<VibeDataLogRepository> _logger;

    private const string CollectionName = "vibe_app";
    private const string DataLogsTable = "data_logs";
    private const string LogSettingsTable = "client_log_settings";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    // Winston numeric levels: { error: 0, warn: 1, info: 2, debug: 3 }
    // Note: Standard Winston has http=3, verbose=4, debug=5, silly=6 but our config uses simplified 4-level
    private static readonly Dictionary<int, string> NumericLevelMap = new()
    {
        [0] = "error",
        [1] = "warn",
        [2] = "info",
        [3] = "debug"
    };

    private static string GetLevelString(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return element.GetString() ?? "unknown";

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var numLevel))
            return NumericLevelMap.TryGetValue(numLevel, out var levelName) ? levelName : $"level_{numLevel}";

        return "unknown";
    }

    public VibeDataLogRepository(VibeDbContext context, ILogger<VibeDataLogRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> CreateLogDocumentAsync(int clientId, Dictionary<string, object?> logData)
    {
        // Get the collection schema for vibe_app
        var schema = await _context.CollectionSchemas
            .Where(s => s.ClientId == clientId && s.Collection == CollectionName && s.IsActive)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync();

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = null, // System logs have no specific owner
            Collection = CollectionName,
            TableName = DataLogsTable,
            Data = JsonSerializer.Serialize(logData, JsonOptions),
            CollectionSchemaId = schema?.CollectionSchemaId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = logData.TryGetValue("created_by", out var createdBy) ? createdBy as int? : null
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        return document.DocumentId;
    }

    public async Task<(List<Dictionary<string, object?>> Items, int Total)> QueryAsync(
        int clientId,
        string? vibeClientId = null,
        string? level = null,
        string? category = null,
        string? collection = null,
        string? errorCode = null,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null,
        int page = 1,
        int limit = 100)
    {
        // Query data_logs documents - we'll filter by client in memory to handle both
        // document client_id and JSON vibe_client_id fields
        var query = _context.Documents
            .Where(d => d.Collection == CollectionName
                     && d.TableName == DataLogsTable
                     && d.DeletedAt == null)
            .AsQueryable();

        // If no vibeClientId provided, filter by document client_id at DB level
        // Otherwise we need to check both, so load more and filter in memory
        if (string.IsNullOrEmpty(vibeClientId))
        {
            query = query.Where(d => d.ClientId == clientId);
        }

        // Apply time filters at database level
        if (since.HasValue)
        {
            query = query.Where(d => d.CreatedAt >= since.Value);
        }

        if (until.HasValue)
        {
            query = query.Where(d => d.CreatedAt <= until.Value);
        }

        // Load documents then filter by JSONB fields in memory
        var documents = await query
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        // Parse and filter in memory
        var filtered = documents.Select(d => new
        {
            Document = d,
            Data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(d.Data ?? "{}")
                   ?? new Dictionary<string, JsonElement>()
        }).AsEnumerable();

        // Filter by client: match document client_id OR JSON vibe_client_id
        if (!string.IsNullOrEmpty(vibeClientId))
        {
            filtered = filtered.Where(x =>
                x.Document.ClientId == clientId ||
                (x.Data.TryGetValue("vibe_client_id", out var v) && v.GetString() == vibeClientId));
        }

        if (!string.IsNullOrEmpty(level))
        {
            filtered = filtered.Where(x =>
                x.Data.TryGetValue("level", out var v) && GetLevelString(v) == level);
        }

        if (!string.IsNullOrEmpty(category))
        {
            filtered = filtered.Where(x =>
                x.Data.TryGetValue("category", out var v) && v.GetString() == category);
        }

        if (!string.IsNullOrEmpty(collection))
        {
            filtered = filtered.Where(x =>
                x.Data.TryGetValue("collection_name", out var v) && v.GetString() == collection);
        }

        if (!string.IsNullOrEmpty(errorCode))
        {
            filtered = filtered.Where(x =>
                x.Data.TryGetValue("error_code", out var v) && v.GetString() == errorCode);
        }

        var filteredList = filtered.ToList();
        var total = filteredList.Count;

        var items = filteredList
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(x =>
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(x.Document.Data ?? "{}", JsonOptions)
                           ?? new Dictionary<string, object?>();
                dict["document_id"] = x.Document.DocumentId;
                return dict;
            }).ToList();

        return (items, total);
    }

    public async Task<LogStats> GetStatsAsync(int clientId, TimeSpan period, string? vibeClientId = null)
    {
        var since = DateTimeOffset.UtcNow - period;

        var query = _context.Documents
            .Where(d => d.Collection == CollectionName
                     && d.TableName == DataLogsTable
                     && d.DeletedAt == null
                     && d.CreatedAt >= since)
            .AsQueryable();

        // Filter by document client_id at DB level if no vibeClientId to check
        if (string.IsNullOrEmpty(vibeClientId))
        {
            query = query.Where(d => d.ClientId == clientId);
        }

        var documents = await query.ToListAsync();

        // Parse and filter in memory
        var parsed = documents.Select(d => new
        {
            Document = d,
            Data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(d.Data ?? "{}")
                   ?? new Dictionary<string, JsonElement>()
        }).AsEnumerable();

        // Filter by client: match document client_id OR JSON vibe_client_id
        if (!string.IsNullOrEmpty(vibeClientId))
        {
            parsed = parsed.Where(x =>
                x.Document.ClientId == clientId ||
                (x.Data.TryGetValue("vibe_client_id", out var v) && v.GetString() == vibeClientId));
        }

        var logs = parsed.Select(x => x.Data).ToList();

        var stats = new LogStats
        {
            Period = period,
            CountsByLevel = logs
                .Where(l => l.TryGetValue("level", out _))
                .GroupBy(l => GetLevelString(l["level"]))
                .ToDictionary(g => g.Key, g => g.Count()),
            CountsByCategory = logs
                .Where(l => l.TryGetValue("category", out _))
                .GroupBy(l => l["category"].GetString() ?? "unknown")
                .ToDictionary(g => g.Key, g => g.Count()),
            TopErrors = logs
                .Where(l => l.TryGetValue("error_code", out var ec) && ec.ValueKind == JsonValueKind.String)
                .GroupBy(l => l["error_code"].GetString()!)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => new ErrorCodeCount { ErrorCode = g.Key, Count = g.Count() })
                .ToList()
        };

        return stats;
    }

    public async Task<string> GetLogLevelAsync(int clientId)
    {
        // Query client_log_settings table in vibe_app
        var settingsDoc = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == LogSettingsTable
                     && d.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (settingsDoc?.Data == null)
            return "info"; // Default

        var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(settingsDoc.Data);
        if (settings != null && settings.TryGetValue("log_level", out var level))
        {
            return level.GetString() ?? "info";
        }

        return "info";
    }

    public async Task SetLogLevelAsync(int clientId, string level)
    {
        var existingDoc = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == LogSettingsTable
                     && d.DeletedAt == null)
            .FirstOrDefaultAsync();

        var settingsData = new Dictionary<string, object?>
        {
            ["log_level"] = level,
            ["updated_at"] = DateTimeOffset.UtcNow.ToString("o")
        };

        if (existingDoc == null)
        {
            // Get schema
            var schema = await _context.CollectionSchemas
                .Where(s => s.ClientId == clientId && s.Collection == CollectionName && s.IsActive)
                .OrderByDescending(s => s.Version)
                .FirstOrDefaultAsync();

            settingsData["created_at"] = DateTimeOffset.UtcNow.ToString("o");

            var newDoc = new VibeDocument
            {
                ClientId = clientId,
                Collection = CollectionName,
                TableName = LogSettingsTable,
                Data = JsonSerializer.Serialize(settingsData, JsonOptions),
                CollectionSchemaId = schema?.CollectionSchemaId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.Documents.Add(newDoc);
        }
        else
        {
            // Update existing
            var existingData = JsonSerializer.Deserialize<Dictionary<string, object?>>(existingDoc.Data ?? "{}")
                               ?? new Dictionary<string, object?>();
            existingData["log_level"] = level;
            existingData["updated_at"] = DateTimeOffset.UtcNow.ToString("o");

            existingDoc.Data = JsonSerializer.Serialize(existingData, JsonOptions);
            existingDoc.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<int> PurgeLogsAsync(int clientId, DateTimeOffset olderThan, string? level = null)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == DataLogsTable
                     && d.DeletedAt == null
                     && d.CreatedAt < olderThan)
            .ToListAsync();

        // Filter by level in memory if specified
        IEnumerable<VibeDocument> docsToDelete = documents;
        if (!string.IsNullOrEmpty(level))
        {
            docsToDelete = documents.Where(d =>
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(d.Data ?? "{}");
                return data != null && data.TryGetValue("level", out var v) && GetLevelString(v) == level;
            });
        }

        var docsList = docsToDelete.ToList();
        var count = docsList.Count;

        if (count > 0)
        {
            // Soft delete
            foreach (var doc in docsList)
            {
                doc.DeletedAt = DateTimeOffset.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("VIBE_LOG_PURGE: Soft-deleted {Count} log documents older than {OlderThan}, level: {Level}, ClientId: {ClientId}",
                count, olderThan, level ?? "all", clientId);
        }

        return count;
    }
}
