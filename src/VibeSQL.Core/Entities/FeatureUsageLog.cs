namespace VibeSQL.Core.Entities;

/// <summary>
/// Tracks usage of metered features for tier-based billing and enforcement.
/// Each record represents usage within a specific period (daily, monthly, etc.).
/// </summary>
public class FeatureUsageLog
{
    public long FeatureUsageLogId { get; set; }

    /// <summary>
    /// The IDP client ID (tenant)
    /// </summary>
    public int ClientId { get; set; }

    /// <summary>
    /// The user ID (null for client-level/service account usage)
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Feature key matching TierFeature.FeatureKey (e.g., "api_calls", "document_creates", "storage_bytes")
    /// </summary>
    public string FeatureKey { get; set; } = string.Empty;

    /// <summary>
    /// Period type: "daily", "weekly", "monthly", "lifetime"
    /// </summary>
    public string PeriodType { get; set; } = "monthly";

    /// <summary>
    /// Start of the usage period (e.g., 2026-01-01 for monthly)
    /// </summary>
    public DateTimeOffset PeriodStart { get; set; }

    /// <summary>
    /// End of the usage period (e.g., 2026-01-31 for monthly)
    /// </summary>
    public DateTimeOffset PeriodEnd { get; set; }

    /// <summary>
    /// Current usage count within this period
    /// </summary>
    public long UsageCount { get; set; }

    /// <summary>
    /// The limit for this period (from TierFeature.LimitValue at time of creation)
    /// -1 = unlimited, 0 = disabled
    /// </summary>
    public int PeriodLimit { get; set; }

    /// <summary>
    /// Whether the limit has been exceeded (for alerting)
    /// </summary>
    public bool LimitExceeded { get; set; }

    /// <summary>
    /// Timestamp of first usage in this period
    /// </summary>
    public DateTimeOffset? FirstUsageAt { get; set; }

    /// <summary>
    /// Timestamp of last usage in this period
    /// </summary>
    public DateTimeOffset? LastUsageAt { get; set; }

    /// <summary>
    /// JSON metadata for additional tracking (e.g., operation types, endpoints)
    /// </summary>
    public string? Metadata { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
