using Microsoft.Extensions.Logging;
using VibeSQL.Sentinel;

namespace VibeSQL.Core.Sentinel;

/// <summary>
/// Data inspector for real PostgreSQL tables.
/// Uses information_schema, pg_class for row estimates, and targeted COUNT(*) for small tables.
/// Boundaries: 5s timeout per query, 500ms ceiling total, timeout = block (never auto-allow when uncertain).
/// </summary>
public class PostgresTableInspector : IDataInspector
{
    private readonly Func<string, CancellationToken, Task<InspectorQueryResult>> _queryFunc;
    private readonly ILogger<PostgresTableInspector>? _logger;

    /// <summary>Tables above this threshold use pg_class.reltuples (approximate) instead of COUNT(*).</summary>
    private const long ExactCountThreshold = 100_000;

    /// <summary>Max total time for all inspector queries.</summary>
    private static readonly TimeSpan TotalBudget = TimeSpan.FromMilliseconds(500);

    /// <summary>Max time per individual query.</summary>
    private static readonly TimeSpan PerQueryTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Create inspector with a query execution function.
    /// The function takes SQL and returns rows + scalar results.
    /// </summary>
    public PostgresTableInspector(
        Func<string, CancellationToken, Task<InspectorQueryResult>> queryFunc,
        ILogger<PostgresTableInspector>? logger = null)
    {
        _queryFunc = queryFunc;
        _logger = logger;
    }

    public async Task<DataInspectionResult> InspectAsync(List<SentinelItem> items, CancellationToken ct = default)
    {
        var result = new DataInspectionResult();
        var startTime = DateTime.UtcNow;

        foreach (var item in items)
        {
            // Budget check
            if (DateTime.UtcNow - startTime > TotalBudget)
            {
                _logger?.LogWarning("SENTINEL_INSPECTOR_BUDGET: Exceeded 500ms ceiling, blocking remaining items");
                // Timeout = block (assume destructive)
                result.Items.Add(new InspectedItem(
                    item, SentinelVerdict.Destructive, DataAtRisk: true, RowCount: -1,
                    Detail: "Inspector budget exceeded — assumed destructive"));
                continue;
            }

            try
            {
                var inspected = item.Code switch
                {
                    SentinelTaxonomy.D300_DropTable => await InspectTableDrop(item, ct),
                    SentinelTaxonomy.D301_DropColumn => await InspectColumnDrop(item, ct),
                    SentinelTaxonomy.D306_NullableToNonNullHasNulls => await InspectNullCount(item, ct),
                    SentinelTaxonomy.D311_TightenConstraint => await InspectRowCount(item, ct),
                    SentinelTaxonomy.D303_IncompatibleTypeCast => await InspectRowCount(item, ct),
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

    private async Task<InspectedItem> InspectTableDrop(SentinelItem item, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(item.TableName))
            return new InspectedItem(item, SentinelVerdict.Destructive, true, -1, "No table name");

        var rowCount = await GetRowCount(item.TableName, ct);

        if (rowCount == 0)
        {
            // Empty table — safe to remove (Migration level)
            return new InspectedItem(item, SentinelVerdict.Migration, DataAtRisk: false, RowCount: 0,
                Detail: $"Table '{item.TableName}' is empty — safe to drop");
        }

        return new InspectedItem(item, SentinelVerdict.Destructive, DataAtRisk: true, RowCount: rowCount,
            Detail: $"Table '{item.TableName}' has {rowCount} row(s)");
    }

    private async Task<InspectedItem> InspectColumnDrop(SentinelItem item, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(item.TableName) || string.IsNullOrEmpty(item.ColumnName))
            return new InspectedItem(item, SentinelVerdict.Destructive, true, -1, "Missing table/column name");

        // Count non-null values in the column
        var sql = $"SELECT COUNT(*) AS cnt FROM \"{EscapeIdentifier(item.TableName)}\" WHERE \"{EscapeIdentifier(item.ColumnName)}\" IS NOT NULL";
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(PerQueryTimeout);

        var result = await _queryFunc(sql, cts.Token);
        var nonNullCount = result.ScalarLong;

        if (nonNullCount == 0)
        {
            return new InspectedItem(item, SentinelVerdict.Migration, DataAtRisk: false, RowCount: 0,
                Detail: $"Column '{item.ColumnName}' on '{item.TableName}' has no non-null values — safe to drop");
        }

        return new InspectedItem(item, SentinelVerdict.Destructive, DataAtRisk: true, RowCount: nonNullCount,
            Detail: $"Column '{item.ColumnName}' on '{item.TableName}' has {nonNullCount} non-null value(s)");
    }

    private async Task<InspectedItem> InspectNullCount(SentinelItem item, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(item.TableName) || string.IsNullOrEmpty(item.ColumnName))
            return new InspectedItem(item, SentinelVerdict.Destructive, true, -1, "Missing table/column name");

        var sql = $"SELECT COUNT(*) AS cnt FROM \"{EscapeIdentifier(item.TableName)}\" WHERE \"{EscapeIdentifier(item.ColumnName)}\" IS NULL";
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(PerQueryTimeout);

        var result = await _queryFunc(sql, cts.Token);
        var nullCount = result.ScalarLong;

        if (nullCount == 0)
        {
            // No nulls — safe to make non-null (Migration)
            return new InspectedItem(item, SentinelVerdict.Migration, DataAtRisk: false, RowCount: 0,
                Detail: $"Column '{item.ColumnName}' on '{item.TableName}' has no NULL values — safe to set NOT NULL");
        }

        return new InspectedItem(item, SentinelVerdict.Destructive, DataAtRisk: true, RowCount: nullCount,
            Detail: $"Column '{item.ColumnName}' on '{item.TableName}' has {nullCount} NULL value(s)");
    }

    private async Task<InspectedItem> InspectRowCount(SentinelItem item, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(item.TableName))
            return new InspectedItem(item, SentinelVerdict.Destructive, true, -1, "No table name");

        var rowCount = await GetRowCount(item.TableName, ct);

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
    private async Task<long> GetRowCount(string tableName, CancellationToken ct)
    {
        // First get approximate count from pg_class
        var approxSql = $"SELECT reltuples::bigint AS cnt FROM pg_class WHERE relname = '{EscapeValue(tableName)}'";
        using var cts1 = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts1.CancelAfter(PerQueryTimeout);

        var approxResult = await _queryFunc(approxSql, cts1.Token);
        var approxCount = approxResult.ScalarLong;

        // If approximate says large, trust it
        if (approxCount > ExactCountThreshold)
        {
            _logger?.LogDebug("SENTINEL_INSPECTOR: Table '{Table}' approximate count {Count} (using pg_class)", tableName, approxCount);
            return approxCount;
        }

        // Small table — get exact count
        var exactSql = $"SELECT COUNT(*) AS cnt FROM \"{EscapeIdentifier(tableName)}\"";
        using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts2.CancelAfter(PerQueryTimeout);

        var exactResult = await _queryFunc(exactSql, cts2.Token);
        return exactResult.ScalarLong;
    }

    private static string EscapeIdentifier(string name) => name.Replace("\"", "\"\"");
    private static string EscapeValue(string value) => value.Replace("'", "''");
}

/// <summary>
/// Result from an inspector SQL query.
/// </summary>
public class InspectorQueryResult
{
    public long ScalarLong { get; init; }
    public List<Dictionary<string, object?>>? Rows { get; init; }

    public static InspectorQueryResult FromScalar(long value) => new() { ScalarLong = value };
}
