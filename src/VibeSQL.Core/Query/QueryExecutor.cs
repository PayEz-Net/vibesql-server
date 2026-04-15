using System.Diagnostics;
using System.Text.RegularExpressions;
using Npgsql;
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
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(timeoutCts.Token);

            if (IsNonQueryDml(sql))
            {
                await using var command = new NpgsqlCommand(sql, connection);
                var affectedRows = await command.ExecuteNonQueryAsync(timeoutCts.Token);

                stopwatch.Stop();

                _logger.LogInformation(
                    "VIBESQL_QUERY: Non-query succeeded - {AffectedRows} rows affected in {ElapsedMs:F2}ms",
                    affectedRows, stopwatch.Elapsed.TotalMilliseconds);

                return new QueryExecutionResult
                {
                    Rows = new List<Dictionary<string, object?>>(),
                    RowCount = affectedRows,
                    ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds
                };
            }

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
        catch (PostgresException pgEx)
        {
            stopwatch.Stop();

            if (SqlStateMapper.IsConstraintViolation(pgEx.SqlState))
            {
                // Structured constraint-enforcement event. The VibeEventType
                // scope property is what the dedicated file/syslog/graylog
                // sub-logger filters on (see Program.cs constraint sink).
                // Keep the message Postgres-like so flatfile readers feel native.
                using (_logger.BeginScope(new Dictionary<string, object?>
                {
                    ["VibeEventType"] = "CONSTRAINT_VIOLATION",
                    ["SqlState"] = pgEx.SqlState,
                    ["ConstraintName"] = pgEx.ConstraintName ?? string.Empty,
                    ["SchemaName"] = pgEx.SchemaName ?? string.Empty,
                    ["TableName"] = pgEx.TableName ?? string.Empty,
                    ["ColumnName"] = pgEx.ColumnName ?? string.Empty,
                    ["Detail"] = pgEx.Detail ?? string.Empty,
                    ["Hint"] = pgEx.Hint ?? string.Empty,
                    ["Statement"] = TruncateForLog(sql, 500),
                    ["DurationMs"] = stopwatch.Elapsed.TotalMilliseconds,
                }))
                {
                    _logger.LogWarning(
                        "CONSTRAINT_VIOLATION [{SqlState}]: {PgMessage} (constraint={Constraint}, table={Schema}.{Table}, column={Column})",
                        pgEx.SqlState,
                        pgEx.MessageText,
                        string.IsNullOrEmpty(pgEx.ConstraintName) ? "-" : pgEx.ConstraintName,
                        string.IsNullOrEmpty(pgEx.SchemaName) ? "-" : pgEx.SchemaName,
                        string.IsNullOrEmpty(pgEx.TableName) ? "-" : pgEx.TableName,
                        string.IsNullOrEmpty(pgEx.ColumnName) ? "-" : pgEx.ColumnName);
                }
            }
            else
            {
                _logger.LogError(pgEx, "VIBESQL_QUERY: PostgreSQL error - {SqlState}: {Message}", pgEx.SqlState, pgEx.MessageText);
            }

            throw SqlStateMapper.TranslatePostgresError(pgEx);
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
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        var results = new List<Dictionary<string, object?>>();

        await using var command = new NpgsqlCommand(sql, connection);
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

    // Regex patterns for DML detection normalization. Duplicated from
    // QuerySafetyChecker on purpose — cross-file refactoring to a shared
    // SqlTextNormalizer is out of scope for this entry. A future refactor
    // can unify both call sites.
    private static readonly Regex SingleLineCommentPattern = new(@"--[^\n]*", RegexOptions.Compiled);
    private static readonly Regex MultiLineCommentPattern = new(@"/\*[\s\S]*?\*/", RegexOptions.Compiled);
    private static readonly Regex StringLiteralPattern = new(@"'([^']|'')*'", RegexOptions.Compiled);

    /// <summary>
    /// Detects DML statements (UPDATE / DELETE / INSERT) that do not have a RETURNING
    /// clause, so they can be routed through ExecuteNonQueryAsync instead of
    /// ExecuteReaderAsync. Raw DML without RETURNING produces no result set, and calling
    /// ExecuteReaderAsync on a result-less command yields a reader with zero columns
    /// and zero rows — correct but misleading (RowCount reports 0 instead of the actual
    /// affected row count the caller almost certainly wants).
    /// </summary>
    /// <remarks>
    /// Strips SQL comments and string literals before matching so that RETURNING
    /// appearing in a comment or inside a string literal does not false-positive
    /// the DML-without-RETURNING check, and so that RETURNING in a literal does not
    /// incorrectly route an UPDATE through the reader path.
    ///
    /// KNOWN LIMITATION: CTE-wrapped DML (e.g. WITH x AS (SELECT 1) UPDATE foo SET ...)
    /// is NOT detected as non-query because the query starts with WITH, not with
    /// UPDATE / DELETE / INSERT. Such queries route through ExecuteReaderAsync and may
    /// silently misbehave (zero rows returned, no affected row count). This matches the
    /// private PayEz.VibeSql.Server.Api implementation exactly — the upstreaming program
    /// is a mechanical port, not a detection-engine upgrade. Fixing this properly requires
    /// real SQL parsing (parsing past leading CTEs to find the actual root statement kind),
    /// which is out of scope. If you need CTE-wrapped DML to work today, rewrite the SQL
    /// to avoid the leading CTE. A test case pins the current (wrong) behavior so it
    /// cannot be silently "fixed" in a future PR without an explicit scope discussion.
    /// </remarks>
    internal static bool IsNonQueryDml(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return false;

        var normalized = StripCommentsAndStringLiterals(sql).TrimStart().ToUpperInvariant();

        var isDml = normalized.StartsWith("UPDATE", StringComparison.Ordinal)
            || normalized.StartsWith("DELETE", StringComparison.Ordinal)
            || normalized.StartsWith("INSERT", StringComparison.Ordinal);

        if (!isDml)
            return false;

        return !normalized.Contains("RETURNING", StringComparison.Ordinal);
    }

    private static string StripCommentsAndStringLiterals(string sql)
    {
        sql = SingleLineCommentPattern.Replace(sql, "");
        sql = MultiLineCommentPattern.Replace(sql, "");
        sql = StringLiteralPattern.Replace(sql, "''");
        return sql;
    }
}
