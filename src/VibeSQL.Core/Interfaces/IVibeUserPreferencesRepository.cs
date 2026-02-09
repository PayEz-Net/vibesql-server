using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for user preferences and GDPR operations.
/// </summary>
public interface IVibeUserPreferencesRepository
{
    /// <summary>
    /// Get email preferences for a user
    /// </summary>
    Task<EmailPreferences?> GetEmailPreferencesAsync(int clientId, int userId);

    /// <summary>
    /// Create or update email preferences with all fields
    /// </summary>
    Task<EmailPreferences> UpsertEmailPreferencesAsync(int clientId, int userId, EmailPreferences preferences);

    /// <summary>
    /// Get user profile data for export (GDPR)
    /// </summary>
    Task<UserProfileExport?> GetUserProfileExportAsync(int clientId, int userId);

    /// <summary>
    /// Delete all user data (GDPR right to be forgotten)
    /// </summary>
    Task<UserDataDeletionResult> DeleteUserDataAsync(int clientId, int userId);
}

/// <summary>
/// User profile data for GDPR export
/// </summary>
public class UserProfileExport
{
    public int UserId { get; set; }
    public string? Email { get; set; }
    public string? TierKey { get; set; }
    public string? TierDisplayName { get; set; }
    public DateTime? SubscriptionStart { get; set; }
    public DateTime? TrialEnd { get; set; }
    public bool HasActiveSubscription { get; set; }
    public EmailPreferences? EmailPreferences { get; set; }
    public int DocumentCount { get; set; }
    public UserCreditsData? Credits { get; set; }
    public List<AuditLog>? ActivityLog { get; set; }
}

/// <summary>
/// User credits data
/// </summary>
public class UserCreditsData
{
    public long AiCredits { get; set; }
    public long StorageCredits { get; set; }
    public DateTime? LastReset { get; set; }
}

/// <summary>
/// Result of user data deletion
/// </summary>
public class UserDataDeletionResult
{
    public bool Success { get; set; }
    public int EmailPreferencesDeleted { get; set; }
    public int DocumentsDeleted { get; set; }
    public int CreditsDeleted { get; set; }
    public int PaymentsDeleted { get; set; }
    public int PurchasesDeleted { get; set; }
    public int AuditLogsAnonymized { get; set; }
}
