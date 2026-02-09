using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for usage metering and feature limits.
/// </summary>
public class VibeUsageRepository : IVibeUsageRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<VibeUsageRepository> _logger;

    public VibeUsageRepository(VibeDbContext context, ILogger<VibeUsageRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<UsageCheckResult> CheckUsageAsync(int clientId, int? userId, string featureKey)
    {
        // Get client's tier
        var tier = await GetClientTierAsync(clientId);
        if (tier == null)
        {
            return new UsageCheckResult
            {
                LimitExceeded = false,
                CurrentUsage = 0,
                Limit = null,
                TierKey = null,
                FeatureKey = featureKey
            };
        }

        // Get feature limit from tier
        var feature = tier.Features?.FirstOrDefault(f => f.FeatureKey == featureKey);
        if (feature == null || !feature.IsEnabled)
        {
            return new UsageCheckResult
            {
                LimitExceeded = true,
                CurrentUsage = 0,
                Limit = 0,
                TierKey = tier.TierKey,
                FeatureKey = featureKey
            };
        }

        // Get current usage
        var currentPeriodStart = GetCurrentPeriodStart(feature.LimitPeriod);
        var usageLog = await _context.FeatureUsageLogs
            .Where(u => u.ClientId == clientId
                     && u.FeatureKey == featureKey
                     && (userId == null || u.UserId == userId)
                     && u.PeriodStart >= currentPeriodStart)
            .FirstOrDefaultAsync();

        var currentUsage = usageLog?.UsageCount ?? 0;
        var limit = feature.LimitValue > 0 ? (long?)feature.LimitValue : null;

        return new UsageCheckResult
        {
            LimitExceeded = limit.HasValue && currentUsage >= limit.Value,
            CurrentUsage = currentUsage,
            Limit = limit,
            TierKey = tier.TierKey,
            FeatureKey = featureKey
        };
    }

    public async Task<UsageCheckResult> IncrementUsageAsync(int clientId, int? userId, string featureKey)
    {
        // Get client's tier
        var tier = await GetClientTierAsync(clientId);
        if (tier == null)
        {
            return new UsageCheckResult
            {
                LimitExceeded = false,
                CurrentUsage = 1,
                Limit = null,
                TierKey = null,
                FeatureKey = featureKey
            };
        }

        // Get feature limit from tier
        var feature = tier.Features?.FirstOrDefault(f => f.FeatureKey == featureKey);
        long? limit = feature?.LimitValue > 0 ? feature.LimitValue : null;

        // Atomic UPSERT for usage tracking
        var currentPeriodStart = GetCurrentPeriodStart(feature?.LimitPeriod);
        var usageLog = await _context.FeatureUsageLogs
            .Where(u => u.ClientId == clientId
                     && u.FeatureKey == featureKey
                     && (userId == null || u.UserId == userId)
                     && u.PeriodStart >= currentPeriodStart)
            .FirstOrDefaultAsync();

        if (usageLog == null)
        {
            usageLog = new FeatureUsageLog
            {
                ClientId = clientId,
                UserId = userId ?? 0,
                FeatureKey = featureKey,
                UsageCount = 1,
                PeriodStart = currentPeriodStart,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _context.FeatureUsageLogs.Add(usageLog);
        }
        else
        {
            usageLog.UsageCount++;
            usageLog.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogDebug("VIBE_USAGE_INCREMENT: Feature={Feature}, Client={Client}, Usage={Usage}",
            featureKey, clientId, usageLog.UsageCount);

        return new UsageCheckResult
        {
            LimitExceeded = limit.HasValue && usageLog.UsageCount > limit.Value,
            CurrentUsage = usageLog.UsageCount,
            Limit = limit,
            TierKey = tier.TierKey,
            FeatureKey = featureKey
        };
    }

    public async Task<Dictionary<string, UsageSummary>> GetUsageSummaryAsync(int clientId, int? userId = null)
    {
        var currentPeriodStart = GetCurrentPeriodStart("monthly");

        var usageLogs = await _context.FeatureUsageLogs
            .Where(u => u.ClientId == clientId
                     && (userId == null || u.UserId == userId)
                     && u.PeriodStart >= currentPeriodStart)
            .ToListAsync();

        var tier = await GetClientTierAsync(clientId);
        var featureLimits = tier?.Features?.ToDictionary(f => f.FeatureKey, f => (long?)f.LimitValue) ?? new();

        return usageLogs.ToDictionary(
            u => u.FeatureKey,
            u => new UsageSummary
            {
                FeatureKey = u.FeatureKey,
                CurrentUsage = u.UsageCount,
                Limit = featureLimits.GetValueOrDefault(u.FeatureKey),
                ResetDate = GetNextResetDate("monthly")
            });
    }

    public async Task<TierConfiguration?> GetClientTierAsync(int clientId)
    {
        // For now, get the default tier. In a full implementation,
        // this would look up the client's assigned tier.
        return await _context.TierConfigurations
            .Include(t => t.Features)
            .FirstOrDefaultAsync(t => t.IsDefault && t.IsActive);
    }

    public async Task ResetMonthlyUsageAsync(int clientId)
    {
        var currentPeriodStart = GetCurrentPeriodStart("monthly");

        var oldUsage = await _context.FeatureUsageLogs
            .Where(u => u.ClientId == clientId && u.PeriodStart < currentPeriodStart)
            .ToListAsync();

        if (oldUsage.Count > 0)
        {
            _context.FeatureUsageLogs.RemoveRange(oldUsage);
            await _context.SaveChangesAsync();

            _logger.LogInformation("VIBE_USAGE_RESET: Cleared {Count} usage records for client {ClientId}",
                oldUsage.Count, clientId);
        }
    }

    private static DateTime GetCurrentPeriodStart(string? limitPeriod)
    {
        var now = DateTime.UtcNow;
        return limitPeriod?.ToLower() switch
        {
            "daily" => now.Date,
            "weekly" => now.Date.AddDays(-(int)now.DayOfWeek),
            "monthly" => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            "yearly" => new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc) // Default to monthly
        };
    }

    private static DateTime GetNextResetDate(string? limitPeriod)
    {
        var now = DateTime.UtcNow;
        return limitPeriod?.ToLower() switch
        {
            "daily" => now.Date.AddDays(1),
            "weekly" => now.Date.AddDays(7 - (int)now.DayOfWeek),
            "monthly" => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1),
            "yearly" => new DateTime(now.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1)
        };
    }
}
