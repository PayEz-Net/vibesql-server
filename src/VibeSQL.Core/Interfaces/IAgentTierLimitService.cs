namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for retrieving agent tier limits from vibe.documents.
/// Tier limits control max agents, teams, and feature access per subscription tier.
/// </summary>
public interface IAgentTierLimitService
{
    /// <summary>
    /// Get tier limits for a specific tier code.
    /// </summary>
    /// <param name="tierCode">Tier code (e.g., "free-trial", "pro", "enterprise")</param>
    /// <returns>Tier limits or null if not found</returns>
    Task<AgentTierLimits?> GetTierLimitsAsync(string tierCode);

    /// <summary>
    /// Get the default tier limits (used when tier code is unknown).
    /// </summary>
    Task<AgentTierLimits> GetDefaultTierLimitsAsync();

    /// <summary>
    /// Check if agent creation is allowed for a user based on their tier.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="userId">User ID</param>
    /// <param name="tierCode">User's tier code</param>
    /// <returns>Result indicating if allowed, with error message if not</returns>
    Task<AgentTierCheckResult> CanCreateAgentAsync(int clientId, int userId, string? tierCode);
}

/// <summary>
/// Agent tier limits configuration.
/// </summary>
public class AgentTierLimits
{
    /// <summary>
    /// Tier code identifier (e.g., "free-trial", "pro")
    /// </summary>
    public string TierCode { get; set; } = string.Empty;

    /// <summary>
    /// Maximum agents allowed for this tier (-1 = unlimited)
    /// </summary>
    public int MaxAgents { get; set; } = 10;

    /// <summary>
    /// Maximum teams allowed for this tier (-1 = unlimited)
    /// </summary>
    public int MaxTeams { get; set; } = 1;

    /// <summary>
    /// Whether agent mail feature is enabled
    /// </summary>
    public bool AgentMailEnabled { get; set; } = true;

    /// <summary>
    /// Whether agent runner feature is enabled
    /// </summary>
    public bool AgentRunnerEnabled { get; set; }

    /// <summary>
    /// Whether kanban feature is enabled
    /// </summary>
    public bool KanbanEnabled { get; set; } = true;

    /// <summary>
    /// Whether architecture rules feature is enabled
    /// </summary>
    public bool ArchitectureRulesEnabled { get; set; } = true;

    /// <summary>
    /// Whether schema designer feature is enabled
    /// </summary>
    public bool SchemaDesignerEnabled { get; set; }

    /// <summary>
    /// Maximum mail messages per day (-1 = unlimited)
    /// </summary>
    public int MaxMailPerDay { get; set; } = 100;

    /// <summary>
    /// Maximum runner sessions per day (-1 = unlimited)
    /// </summary>
    public int MaxRunnerSessionsPerDay { get; set; }

    /// <summary>
    /// Check if this tier has unlimited agents
    /// </summary>
    public bool HasUnlimitedAgents => MaxAgents == -1;
}

/// <summary>
/// Result of tier limit check for agent creation.
/// </summary>
public class AgentTierCheckResult
{
    /// <summary>
    /// Whether agent creation is allowed
    /// </summary>
    public bool Allowed { get; set; }

    /// <summary>
    /// Error message if not allowed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Error code if not allowed
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Current agent count for the user
    /// </summary>
    public int CurrentCount { get; set; }

    /// <summary>
    /// Maximum allowed for the tier
    /// </summary>
    public int MaxAllowed { get; set; }

    /// <summary>
    /// The tier code used for the check
    /// </summary>
    public string? TierCode { get; set; }

    public static AgentTierCheckResult Success(int currentCount, int maxAllowed, string? tierCode) => new()
    {
        Allowed = true,
        CurrentCount = currentCount,
        MaxAllowed = maxAllowed,
        TierCode = tierCode
    };

    public static AgentTierCheckResult LimitReached(int currentCount, int maxAllowed, string? tierCode) => new()
    {
        Allowed = false,
        Error = $"Agent limit reached. Your {tierCode ?? "current"} tier allows {maxAllowed} agents. You have {currentCount}.",
        ErrorCode = "TIER_LIMIT_REACHED",
        CurrentCount = currentCount,
        MaxAllowed = maxAllowed,
        TierCode = tierCode
    };
}
