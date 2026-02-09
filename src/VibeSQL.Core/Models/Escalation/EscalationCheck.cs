namespace VibeSQL.Core.Models.Escalation;

/// <summary>
/// Result of checking escalation triggers.
/// </summary>
public class EscalationCheck
{
    /// <summary>
    /// Whether any trigger condition was met.
    /// </summary>
    public bool ShouldEscalate { get; set; }

    /// <summary>
    /// List of triggers that fired.
    /// </summary>
    public List<EscalationTriggerType> Triggers { get; set; } = new();

    /// <summary>
    /// Primary reason for escalation (first trigger).
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Additional details about the triggers.
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();

    /// <summary>
    /// Creates a check result indicating no escalation needed.
    /// </summary>
    public static EscalationCheck NoEscalation() => new()
    {
        ShouldEscalate = false
    };

    /// <summary>
    /// Creates a check result indicating escalation is needed.
    /// </summary>
    public static EscalationCheck Escalate(EscalationTriggerType trigger, string reason, Dictionary<string, object>? details = null) => new()
    {
        ShouldEscalate = true,
        Triggers = new List<EscalationTriggerType> { trigger },
        Reason = reason,
        Details = details ?? new()
    };
}
