namespace VibeSQL.Core.Entities;

/// <summary>
/// Represents an escalation event from the vibe_agents.escalation_log table.
/// Logs coordinator emergency shutdowns and human notifications.
/// </summary>
public class EscalationLog
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int ProjectId { get; set; }
    public DateTime TriggeredAt { get; set; }
    public int SensitivityLevel { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public Dictionary<string, object>? TriggerDetails { get; set; }
    public string? ShutdownMode { get; set; }
    public List<string>? NotificationChannels { get; set; }
    public DateTime? NotificationSentAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ResolutionAction { get; set; }
    public string? ResolutionNotes { get; set; }
}
