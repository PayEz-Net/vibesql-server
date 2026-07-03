namespace VibeSQL.Core.Query;

/// <summary>
/// Query execution result.
/// </summary>
public class QueryExecutionResult
{
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int RowCount { get; set; }
    public double ExecutionTimeMs { get; set; }
}
