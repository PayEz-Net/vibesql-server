namespace VibeSQL.Core.Models;

/// <summary>
/// Response model for successful query execution
/// </summary>
public class QueryResponse
{
    public bool Success { get; set; } = true;
    public List<Dictionary<string, object?>> Data { get; set; } = new();
    public QueryMetadata Meta { get; set; } = new();
}

/// <summary>
/// Metadata about query execution
/// </summary>
public class QueryMetadata
{
    public int RowCount { get; set; }
    public double ExecutionTimeMs { get; set; }
}
