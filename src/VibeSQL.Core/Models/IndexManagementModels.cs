namespace VibeSQL.Core.Models;

/// <summary>
/// Represents a virtual index definition parsed from schema metadata.
/// Internal model used between Application and Infrastructure layers.
/// </summary>
public class IndexDefinition
{
    public string IndexName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public List<string> Fields { get; set; } = new();
    public string? PartialCondition { get; set; }
    public bool IsUnique { get; set; }
    public string IndexType { get; set; } = "btree";
}

/// <summary>
/// Result of index creation operation
/// </summary>
public class IndexCreationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? PhysicalIndexName { get; set; }
    public int? VirtualIndexId { get; set; }
}

/// <summary>
/// Information about an existing index
/// </summary>
public class IndexInfo
{
    public int VirtualIndexId { get; set; }
    public string IndexName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string PhysicalIndexName { get; set; } = string.Empty;
    public string PartitionName { get; set; } = string.Empty;
    public List<string> Fields { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
}
