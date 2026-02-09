using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for user preferences and GDPR operations.
/// </summary>
public class VibeUserPreferencesRepository : IVibeUserPreferencesRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<VibeUserPreferencesRepository> _logger;

    public VibeUserPreferencesRepository(VibeDbContext context, ILogger<VibeUserPreferencesRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<EmailPreferences?> GetEmailPreferencesAsync(int clientId, int userId)
    {
        return await _context.EmailPreferences
            .Where(p => p.ClientId == clientId && p.UserId == userId)
            .FirstOrDefaultAsync();
    }

    public async Task<EmailPreferences> UpsertEmailPreferencesAsync(int clientId, int userId, EmailPreferences preferences)
    {
        var prefs = await _context.EmailPreferences
            .Where(p => p.ClientId == clientId && p.UserId == userId)
            .FirstOrDefaultAsync();

        if (prefs == null)
        {
            prefs = new EmailPreferences
            {
                ClientId = clientId,
                UserId = userId,
                WelcomeEmails = preferences.WelcomeEmails,
                PaymentReceipts = preferences.PaymentReceipts,
                UsageWarnings = preferences.UsageWarnings,
                TrialReminders = preferences.TrialReminders,
                SecurityAlerts = preferences.SecurityAlerts,
                MarketingEmails = preferences.MarketingEmails,
                ProductUpdates = preferences.ProductUpdates,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _context.EmailPreferences.Add(prefs);
        }
        else
        {
            prefs.WelcomeEmails = preferences.WelcomeEmails;
            prefs.PaymentReceipts = preferences.PaymentReceipts;
            prefs.UsageWarnings = preferences.UsageWarnings;
            prefs.TrialReminders = preferences.TrialReminders;
            prefs.SecurityAlerts = preferences.SecurityAlerts;
            prefs.MarketingEmails = preferences.MarketingEmails;
            prefs.ProductUpdates = preferences.ProductUpdates;
            prefs.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("EMAIL_PREFS_UPSERTED: UserId={UserId}, ClientId={ClientId}", userId, clientId);

        return prefs;
    }

    public async Task<UserProfileExport?> GetUserProfileExportAsync(int clientId, int userId)
    {
        // Get email preferences
        var prefs = await _context.EmailPreferences
            .Where(p => p.ClientId == clientId && p.UserId == userId)
            .FirstOrDefaultAsync();

        // Get document count
        var docCount = await _context.Documents
            .Where(d => d.ClientId == clientId && d.OwnerUserId == userId && d.DeletedAt == null)
            .CountAsync();

        // Get audit logs
        var auditLogs = await _context.AuditLogs
            .Where(a => a.ClientId == clientId && a.TargetId == userId.ToString())
            .OrderByDescending(a => a.CreatedAt)
            .Take(100)
            .ToListAsync();

        // Get user profile from purchases
        var profileSql = @"
            SELECT user_email as Email, tier_granted as TierKey,
                   subscription_status as SubscriptionStatus, trial_end as TrialEnd,
                   created_at as SubscriptionStart
            FROM vibe.purchases
            WHERE client_id = {0} AND user_id = {1}
            LIMIT 1";

        UserProfileExport? profile = null;
        try
        {
            var profileData = await _context.Set<ProfileQueryResult>()
                .FromSqlRaw(profileSql, clientId, userId)
                .FirstOrDefaultAsync();

            if (profileData != null)
            {
                profile = new UserProfileExport
                {
                    UserId = userId,
                    Email = profileData.Email,
                    TierKey = profileData.TierKey,
                    SubscriptionStart = profileData.SubscriptionStart,
                    TrialEnd = profileData.TrialEnd,
                    HasActiveSubscription = profileData.SubscriptionStatus == "active",
                    DocumentCount = docCount,
                    EmailPreferences = prefs,
                    ActivityLog = auditLogs
                };
            }
        }
        catch
        {
            // If purchases table doesn't exist or query fails, return basic profile
            profile = new UserProfileExport
            {
                UserId = userId,
                DocumentCount = docCount,
                EmailPreferences = prefs,
                ActivityLog = auditLogs
            };
        }

        return profile;
    }

    public async Task<UserDataDeletionResult> DeleteUserDataAsync(int clientId, int userId)
    {
        var result = new UserDataDeletionResult();

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Delete email preferences
            result.EmailPreferencesDeleted = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM vibe.email_preferences WHERE client_id = {clientId} AND user_id = {userId}");

            // Delete documents
            result.DocumentsDeleted = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE vibe.documents SET deleted_at = NOW() WHERE client_id = {clientId} AND user_id = {userId} AND deleted_at IS NULL");

            // Delete user credits
            result.CreditsDeleted = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM vibe.user_credits WHERE client_id = {clientId} AND user_id = {userId}");

            // Delete payments
            result.PaymentsDeleted = await _context.Database.ExecuteSqlInterpolatedAsync(
                $@"DELETE FROM vibe.subscription_payments
                   WHERE stripe_customer_id IN (
                       SELECT stripe_customer_id FROM vibe.purchases WHERE client_id = {clientId} AND user_id = {userId}
                   )");

            // Delete purchases
            result.PurchasesDeleted = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM vibe.purchases WHERE client_id = {clientId} AND user_id = {userId}");

            // Anonymize audit logs
            result.AuditLogsAnonymized = await _context.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE vibe.audit_logs
                   SET description = 'Account deleted - data anonymized', new_value = NULL
                   WHERE client_id = {clientId} AND target_id = {userId}::text");

            await transaction.CommitAsync();
            result.Success = true;

            _logger.LogInformation("USER_DATA_DELETED: UserId={UserId}, ClientId={ClientId}, " +
                "Prefs={Prefs}, Docs={Docs}, Credits={Credits}, Payments={Payments}, Purchases={Purchases}, Logs={Logs}",
                userId, clientId, result.EmailPreferencesDeleted, result.DocumentsDeleted,
                result.CreditsDeleted, result.PaymentsDeleted, result.PurchasesDeleted, result.AuditLogsAnonymized);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "USER_DATA_DELETE_ERROR: UserId={UserId}, ClientId={ClientId}", userId, clientId);
            result.Success = false;
        }

        return result;
    }

}
