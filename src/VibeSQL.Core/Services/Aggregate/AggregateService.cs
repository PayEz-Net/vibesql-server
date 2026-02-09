using Microsoft.Extensions.Logging;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Models.Aggregate;

namespace VibeSQL.Core.Services.Aggregate;

/// <summary>
/// Service for aggregate operations on Vibe SQL documents.
/// </summary>
public class AggregateService : IAggregateService
{
    private readonly IAggregateRepository _aggregateRepository;
    private readonly ILogger<AggregateService> _logger;

    // Vibe SQL uses client_id = 0 for system-level collections
    private const int SystemClientId = 0;

    public AggregateService(
        IAggregateRepository aggregateRepository,
        ILogger<AggregateService> logger)
    {
        _aggregateRepository = aggregateRepository;
        _logger = logger;
    }

    public async Task<AggregateResult> SumAsync(
        string collection,
        string tableName,
        string field,
        Dictionary<string, string>? filters = null)
    {
        if (string.IsNullOrWhiteSpace(collection))
        {
            throw new ArgumentException("Collection is required", nameof(collection));
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required", nameof(tableName));
        }

        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException("Field is required", nameof(field));
        }

        try
        {
            var (sum, count) = await _aggregateRepository.SumAsync(
                SystemClientId,
                collection,
                tableName,
                field,
                filters);

            _logger.LogInformation(
                "Sum aggregation: {Collection}.{Table}.{Field} = {Sum} ({Count} documents)",
                collection, tableName, field, sum, count);

            return new AggregateResult
            {
                Operation = "sum",
                Field = field,
                Result = sum,
                Count = count,
                Collection = collection,
                Table = tableName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to calculate sum for {Collection}.{Table}.{Field}",
                collection, tableName, field);
            throw;
        }
    }

    public async Task<AggregateResult> CountAsync(
        string collection,
        string tableName,
        Dictionary<string, string>? filters = null)
    {
        if (string.IsNullOrWhiteSpace(collection))
        {
            throw new ArgumentException("Collection is required", nameof(collection));
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required", nameof(tableName));
        }

        try
        {
            var count = await _aggregateRepository.CountAsync(
                SystemClientId,
                collection,
                tableName,
                filters);

            _logger.LogInformation(
                "Count aggregation: {Collection}.{Table} = {Count} documents",
                collection, tableName, count);

            return new AggregateResult
            {
                Operation = "count",
                Field = string.Empty,
                Result = count,
                Count = count,
                Collection = collection,
                Table = tableName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to count documents for {Collection}.{Table}",
                collection, tableName);
            throw;
        }
    }

    public async Task<AggregateResult> AverageAsync(
        string collection,
        string tableName,
        string field,
        Dictionary<string, string>? filters = null)
    {
        if (string.IsNullOrWhiteSpace(collection))
        {
            throw new ArgumentException("Collection is required", nameof(collection));
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required", nameof(tableName));
        }

        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException("Field is required", nameof(field));
        }

        try
        {
            var (average, count) = await _aggregateRepository.AverageAsync(
                SystemClientId,
                collection,
                tableName,
                field,
                filters);

            _logger.LogInformation(
                "Average aggregation: {Collection}.{Table}.{Field} = {Average} ({Count} documents)",
                collection, tableName, field, average, count);

            return new AggregateResult
            {
                Operation = "average",
                Field = field,
                Result = average,
                Count = count,
                Collection = collection,
                Table = tableName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to calculate average for {Collection}.{Table}.{Field}",
                collection, tableName, field);
            throw;
        }
    }

    public async Task<AggregateResult> MinAsync(
        string collection,
        string tableName,
        string field,
        Dictionary<string, string>? filters = null)
    {
        if (string.IsNullOrWhiteSpace(collection))
        {
            throw new ArgumentException("Collection is required", nameof(collection));
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required", nameof(tableName));
        }

        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException("Field is required", nameof(field));
        }

        try
        {
            var (min, count) = await _aggregateRepository.MinAsync(
                SystemClientId,
                collection,
                tableName,
                field,
                filters);

            _logger.LogInformation(
                "Min aggregation: {Collection}.{Table}.{Field} = {Min} ({Count} documents)",
                collection, tableName, field, min, count);

            return new AggregateResult
            {
                Operation = "min",
                Field = field,
                Result = min,
                Count = count,
                Collection = collection,
                Table = tableName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to calculate min for {Collection}.{Table}.{Field}",
                collection, tableName, field);
            throw;
        }
    }

    public async Task<AggregateResult> MaxAsync(
        string collection,
        string tableName,
        string field,
        Dictionary<string, string>? filters = null)
    {
        if (string.IsNullOrWhiteSpace(collection))
        {
            throw new ArgumentException("Collection is required", nameof(collection));
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required", nameof(tableName));
        }

        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException("Field is required", nameof(field));
        }

        try
        {
            var (max, count) = await _aggregateRepository.MaxAsync(
                SystemClientId,
                collection,
                tableName,
                field,
                filters);

            _logger.LogInformation(
                "Max aggregation: {Collection}.{Table}.{Field} = {Max} ({Count} documents)",
                collection, tableName, field, max, count);

            return new AggregateResult
            {
                Operation = "max",
                Field = field,
                Result = max,
                Count = count,
                Collection = collection,
                Table = tableName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to calculate max for {Collection}.{Table}.{Field}",
                collection, tableName, field);
            throw;
        }
    }
}
