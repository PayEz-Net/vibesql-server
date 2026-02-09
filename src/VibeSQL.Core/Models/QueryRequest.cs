namespace VibeSQL.Core.Models;

/// <summary>
/// Request model for SQL query execution
/// </summary>
public class QueryRequest
{
    /// <summary>
    /// The SQL query to execute
    /// </summary>
    public string Sql { get; set; } = string.Empty;
}
