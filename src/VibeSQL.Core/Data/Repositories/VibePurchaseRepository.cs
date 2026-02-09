using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for purchases, subscriptions, and user credits.
/// </summary>
public class VibePurchaseRepository : IVibePurchaseRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<VibePurchaseRepository> _logger;

    public VibePurchaseRepository(VibeDbContext context, ILogger<VibePurchaseRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> SyncUserTierAsync(int userId, string tierKey, string? stripeSubscriptionId = null)
    {
        var activeStatus = "active";
        var rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE vibe.purchases
               SET tier_granted = {tierKey},
                   stripe_subscription_id = {stripeSubscriptionId},
                   subscription_status = {activeStatus},
                   updated_at = NOW()
               WHERE user_id = {userId}");

        if (rowsAffected > 0)
        {
            _logger.LogInformation("PURCHASE_TIER_SYNC: UserId={UserId}, TierKey={TierKey}", userId, tierKey);
        }

        return rowsAffected > 0;
    }

    public async Task RecordSubscriptionPaymentAsync(
        int userId, string stripeSubscriptionId, string stripeInvoiceId, int amountCents, string currency)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO vibe.subscription_payments
               (stripe_customer_id, stripe_invoice_id, stripe_subscription_id, amount_cents, currency, paid_at, created_at)
               SELECT stripe_customer_id, {stripeInvoiceId}, {stripeSubscriptionId}, {amountCents}, {currency}, NOW(), NOW()
               FROM vibe.purchases WHERE user_id = {userId}
               ON CONFLICT (stripe_invoice_id) DO NOTHING");

        _logger.LogInformation("PAYMENT_RECORDED: UserId={UserId}, Invoice={Invoice}, Amount={Amount}",
            userId, stripeInvoiceId, amountCents / 100m);
    }

    public async Task ResetMonthlyCreditsAsync(int userId, long aiCredits, long storageCredits)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE vibe.user_credits
               SET ai_credits = {aiCredits},
                   storage_credits = {storageCredits},
                   reset_at = NOW(),
                   updated_at = NOW()
               WHERE user_id = {userId}");

        _logger.LogInformation("CREDITS_RESET: UserId={UserId}, AI={AI}, Storage={Storage}",
            userId, aiCredits, storageCredits);
    }

    public async Task<string?> GetUserTierKeyAsync(int userId)
    {
        var result = await _context.Set<TierKeyResult>()
            .FromSqlRaw(
                "SELECT tier_granted as TierKey FROM vibe.purchases WHERE user_id = {0} LIMIT 1", userId)
            .FirstOrDefaultAsync();

        return result?.TierKey;
    }

    public async Task<string?> GetUserTierKeyAsync(int clientId, int userId)
    {
        var result = await _context.Set<TierKeyResult>()
            .FromSqlRaw(
                "SELECT tier_granted as TierKey FROM vibe.purchases WHERE client_id = {0} AND user_id = {1} LIMIT 1", clientId, userId)
            .FirstOrDefaultAsync();

        return result?.TierKey;
    }

    public async Task<bool> UpdateUserCreditsAsync(int userId, long? aiCredits = null, long? storageCredits = null)
    {
        if (!aiCredits.HasValue && !storageCredits.HasValue) return false;

        var updates = new List<string>();
        if (aiCredits.HasValue) updates.Add($"ai_credits = {aiCredits.Value}");
        if (storageCredits.HasValue) updates.Add($"storage_credits = {storageCredits.Value}");
        updates.Add("updated_at = NOW()");

        var sql = $"UPDATE vibe.user_credits SET {string.Join(", ", updates)} WHERE user_id = {{0}}";
        var rowsAffected = await _context.Database.ExecuteSqlRawAsync(sql, userId);

        if (rowsAffected > 0)
        {
            _logger.LogInformation("CREDITS_UPDATED: UserId={UserId}, AI={AI}, Storage={Storage}",
                userId, aiCredits, storageCredits);
        }

        return rowsAffected > 0;
    }

    public async Task<bool> AddCreditsAsync(int userId, long aiCreditsToAdd = 0, long storageCreditsToAdd = 0)
    {
        if (aiCreditsToAdd == 0 && storageCreditsToAdd == 0) return false;

        var rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE vibe.user_credits
               SET ai_credits = ai_credits + {aiCreditsToAdd},
                   storage_credits = storage_credits + {storageCreditsToAdd},
                   updated_at = NOW()
               WHERE user_id = {userId}");

        if (rowsAffected > 0)
        {
            _logger.LogInformation("CREDITS_ADDED: UserId={UserId}, AIAdded={AI}, StorageAdded={Storage}",
                userId, aiCreditsToAdd, storageCreditsToAdd);
        }

        return rowsAffected > 0;
    }

    public async Task<(long AiCredits, long StorageCredits)?> GetUserCreditsAsync(int userId)
    {
        var result = await _context.Set<CreditsResult>()
            .FromSqlRaw(
                "SELECT ai_credits as AiCredits, storage_credits as StorageCredits FROM vibe.user_credits WHERE user_id = {0} LIMIT 1", userId)
            .FirstOrDefaultAsync();

        if (result == null) return null;
        return (result.AiCredits, result.StorageCredits);
    }

    public async Task ResetUserCreditsAsync(int clientId, int userId)
    {
        // Reset credits - upsert to handle case where record doesn't exist
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO vibe.user_credits (client_id, user_id, monthly_used, reset_at, created_at, updated_at)
               VALUES ({clientId}, {userId}, 0, NOW(), NOW(), NOW())
               ON CONFLICT (client_id, user_id) DO UPDATE
               SET monthly_used = 0, reset_at = NOW(), updated_at = NOW()");

        _logger.LogInformation("CREDITS_RESET_ZERO: ClientId={ClientId}, UserId={UserId}", clientId, userId);
    }

    public async Task<bool> ExtendTrialAsync(int userId, DateTime newTrialEnd)
    {
        var rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE vibe.purchases
               SET trial_end = {newTrialEnd},
                   updated_at = NOW()
               WHERE user_id = {userId}");

        if (rowsAffected > 0)
        {
            _logger.LogInformation("TRIAL_EXTENDED: UserId={UserId}, NewTrialEnd={TrialEnd}", userId, newTrialEnd);
        }

        return rowsAffected > 0;
    }

    public async Task<bool> ExtendTrialAsync(int clientId, int userId, DateTime newTrialEnd)
    {
        var rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE vibe.purchases
               SET trial_end = {newTrialEnd},
                   updated_at = NOW()
               WHERE client_id = {clientId} AND user_id = {userId}");

        if (rowsAffected > 0)
        {
            _logger.LogInformation("TRIAL_EXTENDED: ClientId={ClientId}, UserId={UserId}, NewTrialEnd={TrialEnd}",
                clientId, userId, newTrialEnd);
        }

        return rowsAffected > 0;
    }

    public async Task<Dictionary<string, int>> GetActiveSubscriptionCountByTierAsync()
    {
        var results = await _context.Set<SubscriptionCountResult>()
            .FromSqlRaw(
                @"SELECT tier_granted as TierKey, COUNT(*)::int as Count
                  FROM vibe.purchases
                  WHERE subscription_status = 'active' AND stripe_subscription_id IS NOT NULL
                  GROUP BY tier_granted")
            .ToListAsync();

        return results.ToDictionary(r => r.TierKey ?? "unknown", r => r.Count);
    }

    public async Task<long> GetTotalRevenueAsync(DateTime? since = null)
    {
        string sql;
        if (since.HasValue)
        {
            sql = "SELECT COALESCE(SUM(amount_cents), 0)::bigint as Total FROM vibe.subscription_payments WHERE paid_at >= {0}";
            var result = await _context.Set<RevenueTotalResult>()
                .FromSqlRaw(sql, since.Value)
                .FirstOrDefaultAsync();
            return (long)(result?.Total ?? 0);
        }
        else
        {
            sql = "SELECT COALESCE(SUM(amount_cents), 0)::bigint as Total FROM vibe.subscription_payments";
            var result = await _context.Set<RevenueTotalResult>()
                .FromSqlRaw(sql)
                .FirstOrDefaultAsync();
            return (long)(result?.Total ?? 0);
        }
    }
}
