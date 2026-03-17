namespace VibeSQL.Core.Sentinel;

/// <summary>
/// Inspects live database to determine data-at-risk for schema changes.
/// Called only for items with RequiresDataCheck = true.
/// </summary>
public interface IDataInspector
{
    /// <summary>
    /// Inspect data for items that need a data check before final verdict.
    /// Returns updated items with data-aware severity (e.g., empty table drop → Migration, not Destructive).
    /// </summary>
    Task<DataInspectionResult> InspectAsync(List<SentinelItem> items, CancellationToken ct = default);
}

public class DataInspectionResult
{
    /// <summary>Items with updated severity based on data inspection.</summary>
    public List<InspectedItem> Items { get; init; } = new();

    /// <summary>True if any inspected item has data at risk.</summary>
    public bool HasDataAtRisk => Items.Any(i => i.DataAtRisk);

    /// <summary>Total rows across all at-risk tables/columns.</summary>
    public long TotalRowsAtRisk => Items.Where(i => i.DataAtRisk).Sum(i => i.RowCount);
}

public record InspectedItem(
    SentinelItem Original,
    SentinelVerdict ResolvedLevel,
    bool DataAtRisk,
    long RowCount,
    string? Detail = null
);
