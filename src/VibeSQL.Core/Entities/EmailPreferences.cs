namespace VibeSQL.Core.Entities;

/// <summary>
/// User email notification preferences.
/// Controls which email types the user opts in to receive.
/// </summary>
public class EmailPreferences
{
    public long EmailPreferencesId { get; set; }

    /// <summary>
    /// The IDP client identifier (tenant)
    /// </summary>
    public int ClientId { get; set; }

    /// <summary>
    /// The user ID
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Receive welcome emails on registration
    /// </summary>
    public bool WelcomeEmails { get; set; } = true;

    /// <summary>
    /// Receive payment receipt emails
    /// </summary>
    public bool PaymentReceipts { get; set; } = true;

    /// <summary>
    /// Receive usage warning emails (approaching credit limits)
    /// </summary>
    public bool UsageWarnings { get; set; } = true;

    /// <summary>
    /// Receive trial ending reminder emails
    /// </summary>
    public bool TrialReminders { get; set; } = true;

    /// <summary>
    /// Receive security alert emails (login from new device, password changes)
    /// </summary>
    public bool SecurityAlerts { get; set; } = true;

    /// <summary>
    /// Receive marketing/promotional emails
    /// </summary>
    public bool MarketingEmails { get; set; } = false;

    /// <summary>
    /// Receive product update/feature announcement emails
    /// </summary>
    public bool ProductUpdates { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
