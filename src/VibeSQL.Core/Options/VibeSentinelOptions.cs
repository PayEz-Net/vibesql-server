namespace VibeSQL.Core.Options;

/// <summary>
/// Configuration options for the VibeSQL Schema Sentinel (VS-SS).
/// </summary>
public class VibeSentinelOptions
{
    /// <summary>
    /// Enable or disable the Schema Sentinel pipeline.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Block Destructive (D-300) changes without explicit override.
    /// Default: true
    /// </summary>
    public bool BlockDestructive { get; set; } = true;

    /// <summary>
    /// Block Prohibited (P-400) changes. These can never be overridden.
    /// Default: true
    /// </summary>
    public bool BlockProhibited { get; set; } = true;

    /// <summary>
    /// Timeout in seconds for synchronous M-200 migration DDL execution.
    /// Default: 30 seconds
    /// </summary>
    public int MigrationTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Total budget in milliseconds for all data inspector queries.
    /// Exceeding this blocks remaining items (assumes destructive).
    /// Default: 500ms
    /// </summary>
    public int InspectorBudgetMs { get; set; } = 500;

    /// <summary>
    /// Maximum time per individual inspector query.
    /// Timeout = block (assumes destructive).
    /// Default: 5000ms
    /// </summary>
    public int PerQueryTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// If true, Destructive changes remain blocked when no IDataInspector is registered.
    /// This is the safe default - without an inspector, downgrade logic is unavailable.
    /// Default: true
    /// </summary>
    public bool RequireInspectorForDestructive { get; set; } = true;

    /// <summary>
    /// Threshold for using approximate row counts (pg_class.reltuples) vs exact COUNT(*).
    /// Tables above this size use approximation for performance.
    /// Default: 100,000 rows
    /// </summary>
    public long ExactCountThreshold { get; set; } = 100_000;
}
