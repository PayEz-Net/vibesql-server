using System.Diagnostics;
using Devart.Data.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VibeSQL.Core.Query;

/// <summary>
/// Query execution result
/// </summary>
public class QueryExecutionResult
{
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int RowCount { get; set; }
    public double ExecutionTimeMs { get; set; }
}

/// <summary>
/// Executes SQL queries with validation, safety checks, and limits
/// </summary>
public interface IQueryExecutor
{
    Task<QueryExecutionResult> ExecuteAsync(string sql, string? tier = null, CancellationToken cancellationToken = default);
}

public class QueryExecutor : IQueryExecutor
{
    private readonly string _connectionString;
    private readonly IQueryValidator _validator;
    private readonly IQuerySafetyChecker _safetyChecker;
    private readonly IQueryLimiter _limiter;
    private readonly ILogger<QueryExecutor> _logger;

    public QueryExecutor(
        IConfiguration configuration,
        IQueryValidator validator,
        IQuerySafetyChecker safetyChecker,
        IQueryLimiter limiter,
        ILogger<QueryExecutor> logger)
    {
        _connectionString = configuration.GetConnectionString("VibeDb")
            ?? throw new InvalidOperationException("VibeDb connection string not configured");
        _validator = validator;
        _safetyChecker = safetyChecker;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<QueryExecutionResult> ExecuteAsync(string sql, string? tier = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _validator.Validate(sql);
        _safetyChecker.CheckSafety(sql);

        _logger.LogInformation("VIBESQL_QUERY: Executing query: {Query}", TruncateForLog(sql, 100));

        var timeout = _limiter.GetTimeout(tier);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await using var connection = new PgSqlConnection(_connectionString);
            await connection.OpenAsync(timeoutCts.Token);

            var rows = await ExecuteQueryAsync(connection, sql, timeoutCts.Token);

            stopwatch.Stop();

            _logger.LogInformation("VIBESQL_QUERY: Query succeeded - {RowCount} rows in {ElapsedMs:F2}ms",
                rows.Count, stopwatch.Elapsed.TotalMilliseconds);

            return new QueryExecutionResult
            {
                Rows = rows,
                RowCount = rows.Count,
                ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds
            };
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new VibeQueryError(
                VibeErrorCodes.QueryTimeout,
                "Query execution timeout",
                $"Query exceeded the maximum execution time of {timeout.TotalSeconds} seconds");
        }
        catch (PgSqlException pgEx)
        {
            _logger.LogError(pgEx, "VIBESQL_QUERY: PostgreSQL error - {Code}: {Message}", pgEx.ErrorCode, pgEx.Message);
            throw SqlStateMapper.TranslateDevartError(pgEx);
        }
        catch (VibeQueryError)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VIBESQL_QUERY: Unexpected error executing query");
            throw new VibeQueryError(
                VibeErrorCodes.InternalError,
                "An internal error occurred",
                ex.Message);
        }
    }

    private async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(
        PgSqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        var results = new List<Dictionary<string, object?>>();

        await using var command = new PgSqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var columnCount = reader.FieldCount;
        var columnNames = new string[columnCount];
        for (int i = 0; i < columnCount; i++)
        {
            columnNames[i] = reader.GetName(i);
        }

        while (await reader.ReadAsync(cancellationToken))
        {
            _limiter.CheckRowLimit(results.Count);

            var row = new Dictionary<string, object?>();
            for (int i = 0; i < columnCount; i++)
            {
                var value = reader.GetValue(i);
                row[columnNames[i]] = ConvertValue(value);
            }
            results.Add(row);
        }

        return results;
    }

    private static object? ConvertValue(object value)
    {
        if (value == DBNull.Value)
            return null;

        return value switch
        {
            byte[] bytes => Convert.ToBase64String(bytes),
            DateTime dt => dt.ToString("O"),
            DateTimeOffset dto => dto.ToString("O"),
            TimeSpan ts => ts.ToString(),
            Guid guid => guid.ToString(),
            _ => value
        };
    }

    private static string TruncateForLog(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..maxLength] + "...";
    }
}
