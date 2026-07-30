using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using VibeSQL.Core.Options;
using VibeSQL.Core.Sentinel;

namespace VibeSQL.Core.Sentinel;

/// <summary>
/// Data inspector for real PostgreSQL tables.
/// Uses information_schema, pg_class for row estimates, and targeted COUNT(*) for small tables.
/// Boundaries: 5s timeout per query, 500ms ceiling total, timeout = block (never auto-allow when uncertain).
/// </summary>
public class PostgresTableInspector : IDataInspector
{
    private readonly string _connectionString;
    private readonly IOptions<VibeSentinelOptions> _options;
    private readonly ILogger<PostgresTableInspector>? _logger;

    private VibeSentinelOptions Options => _options.Value;

    /// <summary>
    /// Create inspector with a PostgreSQL connection string.
    /// </summary>
    public PostgresTableInspector(
        string connectionString,
        IOptions<VibeSentinelOptions> options,
        ILogger<PostgresTableInspector>? logger = null)
    {
        _connectionString = connectionString;
        _options = options;
        _logger = logger;
    }

    public async Task<DataInspectionResult> InspectAsync(List<SentinelItem> items, CancellationToken ct = default)
    {
        var result = new DataInspectionResult();
        var startTime = DateTime.UtcNow;
        var totalBudget = TimeSpan.FromMilliseconds(Options.InspectorBudgetMs);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        foreach (var item in items)
        {
            // Budget check
            if (DateTime.UtcNow - startTime > totalBudget)
            {
                _logger?.LogWarning("SENTINEL_INSPECTOR_BUDGET: Exceeded {BudgetMs}ms ceiling, blocking remaining items",
                    Options.InspectorBudgetMs);
                // Timeout = block (assume destructive)
                result.Items.Add(new InspectedItem(
                    item, SentinelVerdict.Destructive, DataAtRisk: true, RowCount: -1,
                    Detail: $"Inspector budget exceeded ({Options.InspectorBudgetMs}ms) — assumed destructive"));
                continue;
            }

            try
            {
                var inspected = item.Code switch
                {
                    SentinelTaxonomy.D300_DropTable => await InspectTableDrop(item, connection, ct),
                    SentinelTaxonomy.D301_DropColumn => await InspectColumnDrop(item, connection, ct),
                    SentinelTaxonomy.D306_NullableToNonNullHasNulls => await InspectNullCount(item, connection, ct),
                    SentinelTaxonomy.D311_TightenConstraint => await InspectRowCount(item, connection, ct),
                    SentinelTaxonomy.D303_IncompatibleTypeCast => await InspectRowCount(item, connection, ct),
                    _ => new InspectedItem(item, item.Level, DataAtRisk: false, RowCount: 0,
                        Detail: "No data check needed for this code"),
                };
                result.Items.Add(inspected);
            }
            catch (OperationCanceledException)
            {
                // Timeout = block
                result.Items.Add(new InspectedItem(
                    item, SentinelVerdict.Destructive, DataAtRisk: true, RowCount: -1,
                    Detail: "Query timed out — assumed destructive"));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "SENTINEL_INSPECTOR_ERROR: {Code} {Table}", item.Code, item.TableName);
                // Error = block
                result.Items.Add(new InspectedItem(
                    item, SentinelVerdict.Destructive, DataAtRisk: true, RowCount: -1,
                    Detail: $"Inspector error: {ex.Message}"));
            }
        }

        return result;
    }

    private async Task<InspectedItem> InspectTableDrop(SentinelItem item, NpgsqlConnection connection, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(item.TableName))
            return new InspectedItem(item, SentinelVerdict.Destructive, true, -1, "No table name");

        var rowCount = await GetRowCount(item.TableName, connection, ct);

        if (rowCount == 0)
        {
            // Empty table — safe to remove (Migration level)
            return new InspectedItem(item, SentinelVerdict.Migration, DataAtRisk: false, RowCount: 0,
                Detail: $"Table '{item.TableName}' is empty — safe to drop");
        }

        return new InspectedItem(item, SentinelVerdict.Destructive, DataAtRisk: true, RowCount: rowCount,
            Detail: $"Table '{item.TableName}' has {rowCount} row(s)");
    }

    private async Task<InspectedItem> InspectColumnDrop(SentinelItem item, NpgsqlConnection connection, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(item.TableName) || string.IsNullOrEmpty(item.ColumnName))
            return new InspectedItem(item, SentinelVerdict.Destructive, true, -1, "Missing table/column name");

        // Count non-null values in the column
        var sql = $"SELECT COUNT(*) AS cnt FROM \"{EscapeIdentifier(item.TableName)}\" WHERE \"{EscapeIdentifier(item.ColumnName)}\" IS NOT NULL";
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMilliseconds(Options.PerQueryTimeoutMs));

        var result = await ExecuteScalarAsync(sql, connection, cts.Token);

        if (result == 0)
        {
            return new InspectedItem(item, SentinelVerdict.Migration, DataAtRisk: false, RowCount: 0,
                Detail: $"Column '{item.ColumnName}' on '{item.TableName}' has no non-null values — safe to drop");
        }

        return new InspectedItem(item, SentinelVerdict.Destructive, DataAtRisk: true, RowCount: result,
            Detail: $"Column '{item.ColumnName}' on '{item.TableName}' has {result} non-null value(s)");
    }

    private async Task<InspectedItem> InspectNullCount(SentinelItem item, NpgsqlConnection connection, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(item.TableName) || string.IsNullOrEmpty(item.ColumnName))
            return new InspectedItem(item, SentinelVerdict.Destructive, true, -1, "Missing table/column name");

        var sql = $"SELECT COUNT(*) AS cnt FROM \"{EscapeIdentifier(item.TableName)}\" WHERE \"{EscapeIdentifier(item.ColumnName)}\" IS NULL";
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMilliseconds(Options.PerQueryTimeoutMs));

        var result = await ExecuteScalarAsync(sql, connection, cts.Token);

        if (result == 0)
        {
            // No nulls — safe to make non-null (Migration)
            return new InspectedItem(item, SentinelVerdict.Migration, DataAtRisk: false, RowCount: 0,
                Detail: $"Column '{item.ColumnName}' on '{item.TableName}' has no NULL values — safe to set NOT NULL");
        }

        return new InspectedItem(item, SentinelVerdict.Destructive, DataAtRisk: true, RowCount: result,
            Detail: $"Column '{item.ColumnName}' on '{item.TableName}' has {result} NULL value(s)");
    }

    private async Task<InspectedItem> InspectRowCount(SentinelItem item, NpgsqlConnection connection, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(item.TableName))
            return new InspectedItem(item, SentinelVerdict.Destructive, true, -1, "No table name");

        var rowCount = await GetRowCount(item.TableName, connection, ct);

        if (rowCount == 0)
        {
            return new InspectedItem(item, SentinelVerdict.Migration, DataAtRisk: false, RowCount: 0,
                Detail: $"Table '{item.TableName}' is empty — change is safe");
        }

        return new InspectedItem(item, SentinelVerdict.Destructive, DataAtRisk: true, RowCount: rowCount,
            Detail: $"Table '{item.TableName}' has {rowCount} row(s) — change affects existing data");
    }

    /// <summary>
    /// Get row count: use pg_class.reltuples for large tables, exact COUNT(*) for small ones.
    /// </summary>
    private async Task<long> GetRowCount(string tableName, NpgsqlConnection connection, CancellationToken ct)
    {
        // First get approximate count from pg_class
        var approxSql = $"SELECT reltuples::bigint AS cnt FROM pg_class WHERE relname = '{EscapeValue(tableName)}'";
        using var cts1 = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts1.CancelAfter(TimeSpan.FromMilliseconds(Options.PerQueryTimeoutMs));

        var approxCount = await ExecuteScalarAsync(approxSql, connection, cts1.Token);

        // If approximate says large, trust it
        if (approxCount > Options.ExactCountThreshold)
        {
            _logger?.LogDebug("SENTINEL_INSPECTOR: Table '{Table}' approximate count {Count} (using pg_class)", tableName, approxCount);
            return approxCount;
        }

        // Small table — get exact count
        var exactSql = $"SELECT COUNT(*) AS cnt FROM \"{EscapeIdentifier(tableName)}\"";
        using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts2.CancelAfter(TimeSpan.FromMilliseconds(Options.PerQueryTimeoutMs));

        return await ExecuteScalarAsync(exactSql, connection, cts2.Token);
    }

    private async Task<long> ExecuteScalarAsync(string sql, NpgsqlConnection connection, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, connection);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result);
    }

    private static string EscapeIdentifier(string name) => name.Replace("\"", "\"\"");
    private static string EscapeValue(string value) => value.Replace("'", "''");
}
