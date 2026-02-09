namespace VibeSQL.Core.Models.CoordinatorLoop;

/// <summary>
/// Current status of the coordinator loop for a project.
/// </summary>
public class CoordinatorLoopStatus
{
    public int ProjectId { get; set; }
    public bool Enabled { get; set; }
    public bool AutonomyEnabled { get; set; }
    public int? CoordinatorAgentId { get; set; }
    public string? CoordinatorAgentName { get; set; }
    public int IntervalMinutes { get; set; }
    public int IdleThresholdMinutes { get; set; }
    public int ReviewThresholdMinutes { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public int IdleAgentCount { get; set; }
    public int PendingReviewCount { get; set; }
    public int BlockedTaskCount { get; set; }
}
