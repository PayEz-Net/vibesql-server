namespace VibeSQL.Core.Models.CoordinatorLoop;

/// <summary>
/// Result of a single coordinator loop iteration.
/// </summary>
public class CoordinatorLoopResult
{
    public int ProjectId { get; set; }
    public DateTime ExecutedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Actions taken during this iteration.
    /// </summary>
    public List<CoordinatorAction> Actions { get; set; } = new();

    /// <summary>
    /// Number of agents that were idle and assigned work.
    /// </summary>
    public int IdleAgentsAssigned { get; set; }

    /// <summary>
    /// Number of reviewers that were pushed to complete reviews.
    /// </summary>
    public int ReviewersPushed { get; set; }

    /// <summary>
    /// Number of blocked tasks that were escalated or reassigned.
    /// </summary>
    public int BlockedTasksEscalated { get; set; }
}

/// <summary>
/// A single action taken by the coordinator loop.
/// </summary>
public class CoordinatorAction
{
    public string ActionType { get; set; } = string.Empty;
    public int? TargetAgentId { get; set; }
    public string? TargetAgentName { get; set; }
    public int? TaskId { get; set; }
    public string? Description { get; set; }
    public DateTime Timestamp { get; set; }
}
