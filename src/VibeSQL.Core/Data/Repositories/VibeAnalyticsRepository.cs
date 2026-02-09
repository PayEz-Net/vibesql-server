using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for analytics and reporting queries.
/// </summary>
public class VibeAnalyticsRepository : IVibeAnalyticsRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<VibeAnalyticsRepository> _logger;

    public VibeAnalyticsRepository(VibeDbContext context, ILogger<VibeAnalyticsRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<TierDistribution>> GetTierDistributionAsync()
    {
        var results = await _context.Set<TierDistributionRaw>()
            .FromSqlRaw(
                @"SELECT tier_granted as TierKey, COUNT(*)::int as UserCount
                  FROM vibe.purchases
                  GROUP BY tier_granted")
            .ToListAsync();

        // Get tier display names
        var tiers = await _context.TierConfigurations
            .Where(t => t.IsActive)
            .ToDictionaryAsync(t => t.TierKey, t => t.DisplayName);

        return results.Select(r => new TierDistribution
        {
            TierKey = r.TierKey ?? "unknown",
            DisplayName = tiers.GetValueOrDefault(r.TierKey ?? "", r.TierKey),
            UserCount = r.UserCount
        }).ToList();
    }

    public async Task<List<TierDistribution>> GetTierGrowthTrendAsync(int days = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);

        var results = await _context.Set<TierDistributionRaw>()
            .FromSqlRaw(
                @"SELECT tier_granted as TierKey, COUNT(*)::int as UserCount
                  FROM vibe.purchases
                  WHERE created_at >= {0}
                  GROUP BY tier_granted", cutoff)
            .ToListAsync();

        var tiers = await _context.TierConfigurations
            .Where(t => t.IsActive)
            .ToDictionaryAsync(t => t.TierKey, t => t.DisplayName);

        return results.Select(r => new TierDistribution
        {
            TierKey = r.TierKey ?? "unknown",
            DisplayName = tiers.GetValueOrDefault(r.TierKey ?? "", r.TierKey),
            UserCount = r.UserCount
        }).ToList();
    }

    public async Task<Dictionary<string, int>> GetActiveSubscriptionsByTierAsync()
    {
        var results = await _context.Set<SubscriptionCountRaw>()
            .FromSqlRaw(
                @"SELECT tier_granted as TierKey, COUNT(*)::int as SubscriberCount
                  FROM vibe.purchases
                  WHERE subscription_status = 'active' AND stripe_subscription_id IS NOT NULL
                  GROUP BY tier_granted")
            .ToListAsync();

        return results.ToDictionary(r => r.TierKey ?? "unknown", r => r.SubscriberCount);
    }

    public async Task<decimal> GetTotalRevenueAsync(DateTime? since = null, string? currency = "usd")
    {
        long totalCents;
        if (since.HasValue)
        {
            var result = await _context.Set<RevenueTotalRaw>()
                .FromSqlRaw(
                    @"SELECT COALESCE(SUM(amount_cents), 0)::bigint as TotalCents
                      FROM vibe.subscription_payments
                      WHERE paid_at >= {0}", since.Value)
                .FirstOrDefaultAsync();
            totalCents = result?.TotalCents ?? 0;
        }
        else
        {
            var result = await _context.Set<RevenueTotalRaw>()
                .FromSqlRaw(
                    @"SELECT COALESCE(SUM(amount_cents), 0)::bigint as TotalCents
                      FROM vibe.subscription_payments")
                .FirstOrDefaultAsync();
            totalCents = result?.TotalCents ?? 0;
        }

        return totalCents / 100m;
    }

    public async Task<Dictionary<string, FeatureUsageStats>> GetFeatureUsageStatsAsync(int clientId, DateTime? since = null)
    {
        var query = _context.AuditLogs
            .Where(a => a.ClientId == clientId && a.IsSuccess);

        if (since.HasValue)
            query = query.Where(a => a.CreatedAt >= since.Value);

        var usageData = await query
            .GroupBy(a => new { a.Category, a.Action })
            .Select(g => new { g.Key.Category, g.Key.Action, Count = g.Count() })
            .ToListAsync();

        return usageData.ToDictionary(
            u => $"{u.Category}/{u.Action}",
            u => new FeatureUsageStats
            {
                Category = u.Category,
                Action = u.Action,
                Count = u.Count
            });
    }

    private class TierDistributionRaw
    {
        public string? TierKey { get; set; }
        public int UserCount { get; set; }
    }

    private class SubscriptionCountRaw
    {
        public string? TierKey { get; set; }
        public int SubscriberCount { get; set; }
    }

    private class RevenueTotalRaw
    {
        public long TotalCents { get; set; }
    }
}
