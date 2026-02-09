namespace VibeSQL.Core.Models.Escalation;

/// <summary>
/// Request to update escalation settings.
/// </summary>
public class EscalationSettingsUpdate
{
    /// <summary>
    /// Sensitivity level: 1=Relaxed, 2=Balanced, 3=Cautious, 4=Strict.
    /// </summary>
    public int? SensitivityLevel { get; set; }

    /// <summary>
    /// Shutdown mode: soft, hard, or pause.
    /// </summary>
    public string? ShutdownMode { get; set; }

    /// <summary>
    /// Email address for notifications.
    /// </summary>
    public string? NotifyEmail { get; set; }

    /// <summary>
    /// Phone number for SMS notifications.
    /// </summary>
    public string? NotifyPhone { get; set; }

    /// <summary>
    /// Webhook URL for Slack/Discord/Teams notifications.
    /// </summary>
    public string? NotifyWebhookUrl { get; set; }

    /// <summary>
    /// Minimum minutes between escalation notifications.
    /// </summary>
    public int? CooldownMinutes { get; set; }
}
