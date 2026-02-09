namespace VibeSQL.Core.Models.Escalation;

/// <summary>
/// Types of escalation triggers based on sensitivity level.
/// </summary>
public enum EscalationTriggerType
{
    // Level 1 - Relaxed (Critical Only)
    SystemFailure,
    DataIntegrity,
    SecurityIncident,
    AllAgentsBlocked,

    // Level 2 - Balanced (Default)
    RepeatedBlocker,
    StaleReview,
    QualityDegradation,
    AgentSpinning,
    BudgetExceeded,

    // Level 3 - Cautious
    AnyBlocker,
    TestFailure,
    BuildFailure,
    AgentIdle,
    UnusualPattern,

    // Level 4 - Strict (Human-in-Loop)
    MajorDecision,
    FileDeletion,
    SchemaChange,
    ExternalApiCall,
    MilestoneCompletion,

    // Manual
    Manual
}
