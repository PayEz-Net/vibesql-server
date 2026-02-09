namespace VibeSQL.Core.Models.Aggregate;

/// <summary>
/// Result of an aggregate operation.
/// </summary>
public class AggregateResult
{
    /// <summary>
    /// The aggregate operation performed (e.g., "sum", "avg", "count")
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// The field that was aggregated
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// The numeric result of the aggregation
    /// </summary>
    public decimal Result { get; set; }

    /// <summary>
    /// Number of documents that contributed to the result
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Collection name
    /// </summary>
    public string Collection { get; set; } = string.Empty;

    /// <summary>
    /// Table name
    /// </summary>
    public string Table { get; set; } = string.Empty;
}
