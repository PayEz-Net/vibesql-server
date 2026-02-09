namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for analytics and reporting queries.
/// </summary>
public interface IVibeAnalyticsRepository
{
    /// <summary>
    /// Get user distribution by tier
    /// </summary>
    Task<List<TierDistribution>> GetTierDistributionAsync();

    /// <summary>
    /// Get user growth trend (users added in last N days per tier)
    /// </summary>
    Task<List<TierDistribution>> GetTierGrowthTrendAsync(int days = 30);

    /// <summary>
    /// Get active subscription counts by tier
    /// </summary>
    Task<Dictionary<string, int>> GetActiveSubscriptionsByTierAsync();

    /// <summary>
    /// Get total revenue
    /// </summary>
    Task<decimal> GetTotalRevenueAsync(DateTime? since = null, string? currency = "usd");

    /// <summary>
    /// Get feature usage statistics
    /// </summary>
    Task<Dictionary<string, FeatureUsageStats>> GetFeatureUsageStatsAsync(int clientId, DateTime? since = null);
}

/// <summary>
/// Tier distribution data
/// </summary>
public class TierDistribution
{
    public string TierKey { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public int UserCount { get; set; }
}

/// <summary>
/// Feature usage statistics
/// </summary>
public class FeatureUsageStats
{
    public string Category { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int Count { get; set; }
}
