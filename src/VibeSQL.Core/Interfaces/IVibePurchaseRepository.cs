namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for purchases, subscriptions, and user credits.
/// Handles Stripe sync and credit management.
/// </summary>
public interface IVibePurchaseRepository
{
    /// <summary>
    /// Sync user tier from Stripe subscription
    /// </summary>
    Task<bool> SyncUserTierAsync(int userId, string tierKey, string? stripeSubscriptionId = null);

    /// <summary>
    /// Record a subscription payment
    /// </summary>
    Task RecordSubscriptionPaymentAsync(int userId, string stripeSubscriptionId, string stripeInvoiceId, int amountCents, string currency);

    /// <summary>
    /// Reset monthly credits for a user
    /// </summary>
    Task ResetMonthlyCreditsAsync(int userId, long aiCredits, long storageCredits);

    /// <summary>
    /// Get user's current tier key
    /// </summary>
    Task<string?> GetUserTierKeyAsync(int userId);

    /// <summary>
    /// Get user's current tier key (with client ID)
    /// </summary>
    Task<string?> GetUserTierKeyAsync(int clientId, int userId);

    /// <summary>
    /// Update user credits
    /// </summary>
    Task<bool> UpdateUserCreditsAsync(int userId, long? aiCredits = null, long? storageCredits = null);

    /// <summary>
    /// Add credits to user (increment)
    /// </summary>
    Task<bool> AddCreditsAsync(int userId, long aiCreditsToAdd = 0, long storageCreditsToAdd = 0);

    /// <summary>
    /// Get user credits
    /// </summary>
    Task<(long AiCredits, long StorageCredits)?> GetUserCreditsAsync(int userId);

    /// <summary>
    /// Reset user credits to zero
    /// </summary>
    Task ResetUserCreditsAsync(int clientId, int userId);

    /// <summary>
    /// Extend trial for a user
    /// </summary>
    Task<bool> ExtendTrialAsync(int userId, DateTime newTrialEnd);

    /// <summary>
    /// Extend trial for a user (with client ID)
    /// </summary>
    Task<bool> ExtendTrialAsync(int clientId, int userId, DateTime newTrialEnd);

    /// <summary>
    /// Get active subscription count by tier
    /// </summary>
    Task<Dictionary<string, int>> GetActiveSubscriptionCountByTierAsync();

    /// <summary>
    /// Get total revenue (sum of payments)
    /// </summary>
    Task<long> GetTotalRevenueAsync(DateTime? since = null);
}
