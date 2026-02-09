namespace VibeSQL.Core.Models.Escalation;

/// <summary>
/// Current escalation settings for a project.
/// </summary>
public class EscalationSettings
{
    public int ProjectId { get; set; }

    /// <summary>
    /// Sensitivity level: 1=Relaxed, 2=Balanced, 3=Cautious, 4=Strict.
    /// </summary>
    public int SensitivityLevel { get; set; }

    /// <summary>
    /// Human-readable sensitivity level name.
    /// </summary>
    public string SensitivityLevelName => SensitivityLevel switch
    {
        1 => "Relaxed",
        2 => "Balanced",
        3 => "Cautious",
        4 => "Strict",
        _ => "Unknown"
    };

    /// <summary>
    /// Shutdown mode: soft, hard, or pause.
    /// </summary>
    public string ShutdownMode { get; set; } = "soft";

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
    public int CooldownMinutes { get; set; }

    /// <summary>
    /// When the last escalation occurred.
    /// </summary>
    public DateTime? LastEscalationAt { get; set; }

    /// <summary>
    /// Total number of escalations for this project.
    /// </summary>
    public int EscalationCount { get; set; }
}
