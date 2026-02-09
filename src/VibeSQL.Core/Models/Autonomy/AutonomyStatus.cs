namespace VibeSQL.Core.Models.Autonomy;

/// <summary>
/// Current status of autonomy mode for a project.
/// </summary>
public class AutonomyStatus
{
    public int ProjectId { get; set; }
    public bool Enabled { get; set; }
    public string Mode { get; set; } = "attended";
    public string StopCondition { get; set; } = "milestone";
    public int? CurrentSpecId { get; set; }
    public string? CurrentMilestone { get; set; }
    public int MaxRuntimeHours { get; set; }
    public DateTime? StartedAt { get; set; }
    public string? NotifyPhone { get; set; }
    public string? NotifyEmail { get; set; }
    public TimeSpan? RunningFor { get; set; }
    public int TasksPending { get; set; }
    public int TasksBlocked { get; set; }
}
