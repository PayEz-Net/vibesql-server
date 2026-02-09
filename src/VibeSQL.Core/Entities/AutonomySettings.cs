namespace VibeSQL.Core.Entities;

/// <summary>
/// Represents autonomy settings from the vibe_agents.autonomy_settings table.
/// </summary>
public class AutonomySettings
{
    public int SettingId { get; set; }
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
    public bool SkipPermissions { get; set; }

    // Coordinator loop settings
    public bool CoordinatorLoopEnabled { get; set; }
    public int CoordinatorLoopIntervalMinutes { get; set; } = 5;
    public int CoordinatorLoopIdleThresholdMinutes { get; set; } = 10;
    public int CoordinatorLoopReviewThresholdMinutes { get; set; } = 15;
    public DateTime? CoordinatorLoopLastRunAt { get; set; }

    // Escalation settings
    public int EscalationSensitivity { get; set; } = 2; // 1=Relaxed, 2=Balanced, 3=Cautious, 4=Strict
    public string EscalationShutdownMode { get; set; } = "soft"; // soft, hard, pause
    public string? NotifyWebhookUrl { get; set; }
    public int EscalationCooldownMinutes { get; set; } = 30;
    public DateTime? LastEscalationAt { get; set; }
    public int EscalationCount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
