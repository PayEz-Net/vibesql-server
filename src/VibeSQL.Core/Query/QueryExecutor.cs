using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace VibeSQL.Core.Query;

/// <summary>
/// Executes SQL queries with validation, safety checks, tenant isolation, and limits.
/// Ported from the hardened PayEz.VibeSql.Server.Api binary.
/// </summary>
public class QueryExecutor : IQueryExecutor
{
    private readonly string? _rlsConnectionString;
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
        _rlsConnectionString = configuration.GetConnectionString("VibeDbRls");
        _validator = validator;
        _safetyChecker = safetyChecker;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<QueryExecutionResult> ExecuteAsync(
        string sql,
        string? tier = null,
        int? clientId = null,
        QueryAuditContext? auditContext = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _validator.Validate(sql);
        _safetyChecker.CheckSafety(sql);

        // 189589: classify BEFORE execution. Null = not a cleanly-identified
        // vibe.documents write = no audit row (never a fabricated one). Reads
        // classify null by construction.
        var auditAction = DocumentWriteAudit.ClassifyDocumentWrite(sql);

        _logger.LogInformation("VIBE_QUERY: Executing query: {Query}", TruncateForLog(sql, 100));

        var timeout = _limiter.GetTimeout(tier);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            if (!clientId.HasValue)
            {
                throw new VibeQueryError(
                    VibeErrorCodes.TenantContextRequired,
                    "Tenant context required",
                    "A query was issued with no resolved client_id. Refusing to run on the privileged owner connection (would bypass row-level security). The trusted proxy must forward the resolved tenant id.");
            }

            if (string.IsNullOrWhiteSpace(_rlsConnectionString))
            {
                throw new VibeQueryError(
                    VibeErrorCodes.InternalError,
                    "Tenant isolation not configured",
                    "A client-scoped query was issued but the RLS connection (VibeDbRls / vibe_rls_user) is not configured on this server. Refusing to run tenant data on the privileged connection.");
            }

            await using var connection = new NpgsqlConnection(_rlsConnectionString);
            await connection.OpenAsync(timeoutCts.Token);

            await using var tx = await connection.BeginTransactionAsync(timeoutCts.Token);

            await using (var setCmd = new NpgsqlCommand(
                "SET LOCAL app.client_id = " + clientId.Value.ToString(CultureInfo.InvariantCulture),
                connection,
                tx))
            {
                await setCmd.ExecuteNonQueryAsync(timeoutCts.Token);
            }

            var upperSql = sql.TrimStart().ToUpperInvariant();
            var isNonReturningDml = (upperSql.StartsWith("UPDATE") ||
                                     upperSql.StartsWith("DELETE") ||
                                     upperSql.StartsWith("INSERT")) &&
                                    !upperSql.Contains("RETURNING");

            if (isNonReturningDml)
            {
                await using var cmd = new NpgsqlCommand(sql, connection, tx);
                var affectedRows = await cmd.ExecuteNonQueryAsync(timeoutCts.Token);

                // 189589: audit INSIDE the transaction - the audit row and the
                // document write commit or roll back together. Fail-closed on
                // missing system user (see DocumentWriteAudit).
                if (auditAction != null)
                {
                    await DocumentWriteAudit.WriteAsync(
                        connection, tx, clientId.Value, auditAction,
                        affectedRows, sql, auditContext, timeoutCts.Token);
                }

                await tx.CommitAsync(timeoutCts.Token);

                stopwatch.Stop();
                _logger.LogInformation(
                    "VIBE_QUERY: Non-query succeeded - {AffectedRows} rows affected in {ElapsedMs:F2}ms",
                    affectedRows,
                    stopwatch.Elapsed.TotalMilliseconds);

                return new QueryExecutionResult
                {
                    Rows = new List<Dictionary<string, object?>>(),
                    RowCount = affectedRows,
                    ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds
                };
            }
            else
            {
                var rows = await ExecuteQueryAsync(connection, tx, sql, timeoutCts.Token);

                // 189589: DML with RETURNING lands here - audit it too (row count =
                // returned rows). Plain SELECTs classify null and never reach this.
                if (auditAction != null)
                {
                    await DocumentWriteAudit.WriteAsync(
                        connection, tx, clientId.Value, auditAction,
                        rows.Count, sql, auditContext, timeoutCts.Token);
                }

                await tx.CommitAsync(timeoutCts.Token);

                stopwatch.Stop();
                _logger.LogInformation(
                    "VIBE_QUERY: Query succeeded - {RowCount} rows in {ElapsedMs:F2}ms",
                    rows.Count,
                    stopwatch.Elapsed.TotalMilliseconds);

                return new QueryExecutionResult
                {
                    Rows = rows,
                    RowCount = rows.Count,
                    ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds
                };
            }
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
            _logger.LogError(pgEx, "VIBE_QUERY: PostgreSQL error - {Code}: {Message}", pgEx.SqlState, pgEx.MessageText);
            throw SqlStateMapper.TranslatePostgresError(pgEx);
        }
        catch (VibeQueryError)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VIBE_QUERY: Unexpected error executing query");
            throw new VibeQueryError(
                VibeErrorCodes.InternalError,
                "An internal error occurred",
                ex.Message);
        }
    }

    private async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        var results = new List<Dictionary<string, object?>>();

        await using var command = new NpgsqlCommand(sql, connection, transaction);
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
