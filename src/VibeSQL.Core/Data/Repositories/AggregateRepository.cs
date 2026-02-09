using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for aggregate operations on Vibe SQL JSONB documents.
/// </summary>
public class AggregateRepository : IAggregateRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<AggregateRepository> _logger;

    public AggregateRepository(VibeDbContext context, ILogger<AggregateRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(decimal sum, int count)> SumAsync(
        int clientId,
        string collection,
        string tableName,
        string field,
        Dictionary<string, string>? filters = null)
    {
        // Load all documents for the collection/table
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == collection
                     && d.TableName == tableName
                     && d.DeletedAt == null)
            .ToListAsync();

        if (!documents.Any())
        {
            return (0m, 0);
        }

        // Parse JSONB and apply filters
        var values = new List<decimal>();

        foreach (var document in documents)
        {
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(document.Data);
                if (data == null)
                {
                    continue;
                }

                // Apply filters if provided
                if (filters != null && filters.Any())
                {
                    bool passesFilters = true;
                    foreach (var filter in filters)
                    {
                        if (!data.ContainsKey(filter.Key))
                        {
                            passesFilters = false;
                            break;
                        }

                        var fieldValue = GetFieldValueAsString(data[filter.Key]);
                        if (fieldValue != filter.Value)
                        {
                            passesFilters = false;
                            break;
                        }
                    }

                    if (!passesFilters)
                    {
                        continue;
                    }
                }

                // Check if field exists and extract numeric value
                if (data.ContainsKey(field))
                {
                    var numericValue = ExtractNumericValue(data[field]);
                    if (numericValue.HasValue)
                    {
                        values.Add(numericValue.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse document ID {DocumentId}", document.DocumentId);
                continue;
            }
        }

        var sum = values.Sum();
        var count = values.Count;

        _logger.LogInformation(
            "Calculated sum for {Collection}.{Table}.{Field}: {Sum} across {Count} documents",
            collection, tableName, field, sum, count);

        return (sum, count);
    }

    private string? GetFieldValueAsString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    private decimal? ExtractNumericValue(JsonElement element)
    {
        try
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number => element.TryGetDecimal(out var decValue) ? decValue :
                                       element.TryGetInt64(out var longValue) ? longValue :
                                       element.TryGetDouble(out var doubleValue) ? (decimal)doubleValue : null,
                JsonValueKind.String => decimal.TryParse(element.GetString(), out var parsed) ? parsed : null,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<int> CountAsync(
        int clientId,
        string collection,
        string tableName,
        Dictionary<string, string>? filters = null)
    {
        // Load all documents for the collection/table
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == collection
                     && d.TableName == tableName
                     && d.DeletedAt == null)
            .ToListAsync();

        if (!documents.Any())
        {
            return 0;
        }

        // Apply filters if provided
        int count = 0;
        foreach (var document in documents)
        {
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(document.Data);
                if (data == null)
                {
                    continue;
                }

                // Apply filters if provided
                if (filters != null && filters.Any())
                {
                    bool passesFilters = true;
                    foreach (var filter in filters)
                    {
                        if (!data.ContainsKey(filter.Key))
                        {
                            passesFilters = false;
                            break;
                        }

                        var fieldValue = GetFieldValueAsString(data[filter.Key]);
                        if (fieldValue != filter.Value)
                        {
                            passesFilters = false;
                            break;
                        }
                    }

                    if (!passesFilters)
                    {
                        continue;
                    }
                }

                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse document ID {DocumentId}", document.DocumentId);
                continue;
            }
        }

        _logger.LogInformation(
            "Counted documents for {Collection}.{Table}: {Count}",
            collection, tableName, count);

        return count;
    }

    public async Task<(decimal average, int count)> AverageAsync(
        int clientId,
        string collection,
        string tableName,
        string field,
        Dictionary<string, string>? filters = null)
    {
        var (sum, count) = await SumAsync(clientId, collection, tableName, field, filters);

        if (count == 0)
        {
            return (0m, 0);
        }

        var average = sum / count;

        _logger.LogInformation(
            "Calculated average for {Collection}.{Table}.{Field}: {Average} across {Count} documents",
            collection, tableName, field, average, count);

        return (average, count);
    }

    public async Task<(decimal min, int count)> MinAsync(
        int clientId,
        string collection,
        string tableName,
        string field,
        Dictionary<string, string>? filters = null)
    {
        // Load all documents for the collection/table
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == collection
                     && d.TableName == tableName
                     && d.DeletedAt == null)
            .ToListAsync();

        if (!documents.Any())
        {
            return (0m, 0);
        }

        // Parse JSONB and apply filters
        var values = new List<decimal>();

        foreach (var document in documents)
        {
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(document.Data);
                if (data == null)
                {
                    continue;
                }

                // Apply filters if provided
                if (filters != null && filters.Any())
                {
                    bool passesFilters = true;
                    foreach (var filter in filters)
                    {
                        if (!data.ContainsKey(filter.Key))
                        {
                            passesFilters = false;
                            break;
                        }

                        var fieldValue = GetFieldValueAsString(data[filter.Key]);
                        if (fieldValue != filter.Value)
                        {
                            passesFilters = false;
                            break;
                        }
                    }

                    if (!passesFilters)
                    {
                        continue;
                    }
                }

                // Check if field exists and extract numeric value
                if (data.ContainsKey(field))
                {
                    var numericValue = ExtractNumericValue(data[field]);
                    if (numericValue.HasValue)
                    {
                        values.Add(numericValue.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse document ID {DocumentId}", document.DocumentId);
                continue;
            }
        }

        if (!values.Any())
        {
            return (0m, 0);
        }

        var min = values.Min();
        var count = values.Count;

        _logger.LogInformation(
            "Calculated min for {Collection}.{Table}.{Field}: {Min} across {Count} documents",
            collection, tableName, field, min, count);

        return (min, count);
    }

    public async Task<(decimal max, int count)> MaxAsync(
        int clientId,
        string collection,
        string tableName,
        string field,
        Dictionary<string, string>? filters = null)
    {
        // Load all documents for the collection/table
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == collection
                     && d.TableName == tableName
                     && d.DeletedAt == null)
            .ToListAsync();

        if (!documents.Any())
        {
            return (0m, 0);
        }

        // Parse JSONB and apply filters
        var values = new List<decimal>();

        foreach (var document in documents)
        {
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(document.Data);
                if (data == null)
                {
                    continue;
                }

                // Apply filters if provided
                if (filters != null && filters.Any())
                {
                    bool passesFilters = true;
                    foreach (var filter in filters)
                    {
                        if (!data.ContainsKey(filter.Key))
                        {
                            passesFilters = false;
                            break;
                        }

                        var fieldValue = GetFieldValueAsString(data[filter.Key]);
                        if (fieldValue != filter.Value)
                        {
                            passesFilters = false;
                            break;
                        }
                    }

                    if (!passesFilters)
                    {
                        continue;
                    }
                }

                // Check if field exists and extract numeric value
                if (data.ContainsKey(field))
                {
                    var numericValue = ExtractNumericValue(data[field]);
                    if (numericValue.HasValue)
                    {
                        values.Add(numericValue.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse document ID {DocumentId}", document.DocumentId);
                continue;
            }
        }

        if (!values.Any())
        {
            return (0m, 0);
        }

        var max = values.Max();
        var count = values.Count;

        _logger.LogInformation(
            "Calculated max for {Collection}.{Table}.{Field}: {Max} across {Count} documents",
            collection, tableName, field, max, count);

        return (max, count);
    }
}
