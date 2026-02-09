namespace VibeSQL.Core.Models.Escalation;

/// <summary>
/// Result of triggering an escalation.
/// </summary>
public class EscalationResult
{
    /// <summary>
    /// Whether the escalation was triggered successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The escalation log entry ID.
    /// </summary>
    public int? EscalationId { get; set; }

    /// <summary>
    /// Error message if escalation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether autonomy was disabled.
    /// </summary>
    public bool AutonomyDisabled { get; set; }

    /// <summary>
    /// The shutdown mode used.
    /// </summary>
    public string? ShutdownMode { get; set; }

    /// <summary>
    /// Notification channels used.
    /// </summary>
    public List<string> NotificationChannels { get; set; } = new();

    /// <summary>
    /// Whether in cooldown period (escalation skipped).
    /// </summary>
    public bool InCooldown { get; set; }

    /// <summary>
    /// Creates a success result.
    /// </summary>
    public static EscalationResult Succeeded(int escalationId, string shutdownMode, List<string> channels) => new()
    {
        Success = true,
        EscalationId = escalationId,
        AutonomyDisabled = true,
        ShutdownMode = shutdownMode,
        NotificationChannels = channels
    };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static EscalationResult Failed(string error) => new()
    {
        Success = false,
        ErrorMessage = error
    };

    /// <summary>
    /// Creates a cooldown result (escalation skipped).
    /// </summary>
    public static EscalationResult Cooldown() => new()
    {
        Success = true,
        InCooldown = true
    };
}
