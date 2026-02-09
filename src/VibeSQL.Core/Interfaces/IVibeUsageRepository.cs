using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for usage metering and feature limits.
/// </summary>
public interface IVibeUsageRepository
{
    /// <summary>
    /// Check current usage for a feature
    /// </summary>
    Task<UsageCheckResult> CheckUsageAsync(int clientId, int? userId, string featureKey);

    /// <summary>
    /// Increment usage for a feature (atomic UPSERT)
    /// </summary>
    Task<UsageCheckResult> IncrementUsageAsync(int clientId, int? userId, string featureKey);

    /// <summary>
    /// Get usage summary for a client
    /// </summary>
    Task<Dictionary<string, UsageSummary>> GetUsageSummaryAsync(int clientId, int? userId = null);

    /// <summary>
    /// Get client's current tier
    /// </summary>
    Task<TierConfiguration?> GetClientTierAsync(int clientId);

    /// <summary>
    /// Reset monthly usage counters
    /// </summary>
    Task ResetMonthlyUsageAsync(int clientId);
}

/// <summary>
/// Result of a usage check
/// </summary>
public class UsageCheckResult
{
    public bool LimitExceeded { get; set; }
    public long CurrentUsage { get; set; }
    public long? Limit { get; set; }
    public string? TierKey { get; set; }
    public string? FeatureKey { get; set; }
}

/// <summary>
/// Summary of usage for a feature
/// </summary>
public class UsageSummary
{
    public string FeatureKey { get; set; } = string.Empty;
    public long CurrentUsage { get; set; }
    public long? Limit { get; set; }
    public DateTime? ResetDate { get; set; }
}
